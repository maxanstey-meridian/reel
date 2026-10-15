import { useDocumentVisibility, useTimeoutPoll } from "@vueuse/core";
import { client, unwrap, type PlaybackDesiredState, type PlaybackSession } from "~~/app/lib/api";
import { isPlaybackPending } from "./logic/playback";
import type { CompletedPlayback, Playback } from "./ports/playback";

const isOlderRevision = (next: PlaybackSession, current: PlaybackSession | null): boolean =>
  !!current && next.sessionId === current.sessionId && next.revision < current.revision;

// A poll that was in flight while a control landed may describe the older session;
// keep it only when it is the same session at the same or a later revision.
const isStalePoll = (next: PlaybackSession | null, current: PlaybackSession | null): boolean =>
  !next || !current || next.sessionId !== current.sessionId || isOlderRevision(next, current);

export const usePlaybackSession = () => {
  const session = ref<PlaybackSession | null>(null);
  const completed = ref<CompletedPlayback | null>(null);
  const loading = ref(true);
  const error = shallowRef<Error | null>(null);
  const commandError = ref<string | null>(null);
  const sending = ref(false);

  const fetchSession = async (): Promise<void> => {
    const before = session.value;
    try {
      const next = unwrap(await client.GET("/api/playback/session")).session ?? null;
      if (session.value !== before && isStalePoll(next, session.value)) {
        return;
      }
      if (next?.observation?.state === "ended") {
        completed.value = { relativePath: next.intent.relativePath, title: next.intent.title };
      } else if (next && next.intent.desiredState !== "stopped") {
        completed.value = null;
      }
      session.value = next;
      error.value = null;
    } catch (caught) {
      error.value = caught instanceof Error ? caught : new Error("Couldn't check what's playing");
    } finally {
      loading.value = false;
    }
  };

  // The interval is read when each wait starts, so it stays short until the first answer says whether anything is playing.
  const { pause, resume } = useTimeoutPoll(
    fetchSession,
    () => (session.value || loading.value ? 1_000 : 5_000),
    {
      immediateCallback: true,
    },
  );
  watch(useDocumentVisibility(), (visibility) => (visibility === "visible" ? resume() : pause()));

  const send = async (command: () => Promise<PlaybackSession>): Promise<boolean> => {
    if (sending.value) {
      return false;
    }
    sending.value = true;
    commandError.value = null;
    try {
      const accepted = await command();
      // A poll that answered meanwhile may already show a later revision of this session.
      if (!isOlderRevision(accepted, session.value)) {
        session.value = accepted;
      }
      return true;
    } catch (caught) {
      commandError.value = caught instanceof Error ? caught.message : "The control wasn't sent";
      return false;
    } finally {
      sending.value = false;
    }
  };

  const start = (relativePath: string, startPositionSeconds = 0): Promise<boolean> =>
    send(async () => {
      const started = unwrap(
        await client.POST("/api/playback/session", {
          body: { relativePath, startPositionSeconds },
        }),
      );
      completed.value = null;
      return started;
    });

  const updateIntent = async (
    desiredState: PlaybackDesiredState,
    options: { subtitleIndex?: number | null; seekPositionSeconds?: number } = {},
  ): Promise<boolean> => {
    const current = session.value;
    return (
      !!current &&
      send(async () =>
        unwrap(
          await client.PUT("/api/playback/session/intent", {
            body: {
              sessionId: current.sessionId,
              desiredState,
              subtitleIndex: Object.hasOwn(options, "subtitleIndex")
                ? options.subtitleIndex
                : current.intent.subtitleIndex,
              seekPositionSeconds: options.seekPositionSeconds,
            },
          }),
        ),
      )
    );
  };

  const implementation = {
    session: readonly(session),
    completed: readonly(completed),
    loading: readonly(loading),
    error: shallowReadonly(error),
    commandError: readonly(commandError),
    sending: readonly(sending),
    pending: computed(() => isPlaybackPending(session.value)),
    start,
    updateIntent,
  };
  implementation satisfies Playback;
  return implementation;
};
