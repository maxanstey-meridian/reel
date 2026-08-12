import { computed, onBeforeUnmount, onMounted, readonly, ref } from "vue";
import { api, type PlaybackDesiredState, type PlaybackSession } from "~~/app/lib/api";
import { isPlaybackPending } from "./logic/playback";

const ACTIVE_POLL_MS = 1_000;
const IDLE_POLL_MS = 5_000;
const HIDDEN_ACTIVE_POLL_MS = 5_000;
const HIDDEN_IDLE_POLL_MS = 15_000;

export const usePlaybackSession = () => {
  const session = ref<PlaybackSession | null>(null);
  const loading = ref(true);
  const error = ref<string | null>(null);
  const commandError = ref<string | null>(null);
  const sending = ref(false);
  let timer: ReturnType<typeof setTimeout> | undefined;
  let stopped = false;
  let sessionVersion = 0;
  let pollInFlight: Promise<void> | null = null;
  let commandQueue = Promise.resolve();
  let queuedCommands = 0;
  let commandRequestVersion = 0;

  const pending = computed(() => sending.value || isPlaybackPending(session.value));

  const pollDelay = (): number => {
    const hidden = typeof document !== "undefined" && document.hidden;
    if (hidden) {
      return session.value ? HIDDEN_ACTIVE_POLL_MS : HIDDEN_IDLE_POLL_MS;
    }
    return session.value ? ACTIVE_POLL_MS : IDLE_POLL_MS;
  };

  const fetchSession = async (): Promise<void> => {
    if (pollInFlight) {
      return pollInFlight;
    }
    const version = sessionVersion;
    pollInFlight = (async () => {
      try {
        const envelope = await api.playbackGet();
        if (version === sessionVersion) {
          session.value = envelope.session ?? null;
          error.value = null;
        }
      } catch (caught) {
        if (version === sessionVersion) {
          error.value = caught instanceof Error ? caught.message : "Unable to reach Reel";
        }
      } finally {
        loading.value = false;
        pollInFlight = null;
      }
    })();
    return pollInFlight;
  };

  const schedule = (): void => {
    clearTimeout(timer);
    if (!stopped) {
      timer = setTimeout(runPoll, pollDelay());
    }
  };

  const runPoll = async (): Promise<void> => {
    await fetchSession();
    schedule();
  };

  const runCommand = <T>(command: () => Promise<T>): Promise<T> => {
    queuedCommands++;
    sending.value = true;
    const result = commandQueue.then(command, command);
    commandQueue = result.then(
      () => undefined,
      () => undefined,
    );
    return result.finally(() => {
      queuedCommands--;
      sending.value = queuedCommands > 0;
    });
  };

  const start = async (relativePath: string, startPositionSeconds = 0): Promise<void> => {
    const request = ++commandRequestVersion;
    commandError.value = null;
    try {
      await runCommand(async () => {
        sessionVersion++;
        session.value = await api.playbackStart({ relativePath, startPositionSeconds });
      });
    } catch (caught) {
      if (request === commandRequestVersion) {
        commandError.value = caught instanceof Error ? caught.message : "Playback request failed";
      }
      throw caught;
    } finally {
      schedule();
    }
  };

  const updateIntent = async (
    desiredState: PlaybackDesiredState,
    options: { subtitleIndex?: number | null; seekPositionSeconds?: number } = {},
  ): Promise<void> => {
    const current = session.value;
    if (!current) {
      return;
    }
    const request = ++commandRequestVersion;
    commandError.value = null;
    try {
      await runCommand(async () => {
        const latest = session.value;
        if (!latest) {
          return;
        }
        sessionVersion++;
        session.value = await api.playbackUpdateIntent({
          sessionId: latest.sessionId,
          desiredState,
          subtitleIndex: Object.hasOwn(options, "subtitleIndex")
            ? options.subtitleIndex
            : latest.intent.subtitleIndex,
          seekPositionSeconds: options.seekPositionSeconds,
        });
      });
    } catch (caught) {
      if (request === commandRequestVersion) {
        commandError.value = caught instanceof Error ? caught.message : "Playback control failed";
      }
      throw caught;
    } finally {
      schedule();
    }
  };

  const refreshOnVisibility = (): void => {
    if (!document.hidden) {
      void runPoll();
    } else {
      schedule();
    }
  };

  onMounted(() => {
    stopped = false;
    document.addEventListener("visibilitychange", refreshOnVisibility);
    void runPoll();
  });

  onBeforeUnmount(() => {
    stopped = true;
    clearTimeout(timer);
    document.removeEventListener("visibilitychange", refreshOnVisibility);
  });

  return {
    session: readonly(session),
    loading: readonly(loading),
    error: readonly(error),
    commandError: readonly(commandError),
    sending: readonly(sending),
    pending,
    refresh: fetchSession,
    start,
    updateIntent,
  };
};

export type PlaybackContext = ReturnType<typeof usePlaybackSession>;
