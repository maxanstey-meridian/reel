import type { ComputedRef, DeepReadonly, Ref } from "vue";
import { useProvideInject } from "~~/app/composables/useProvideInject";
import type { PlaybackDesiredState, PlaybackSession } from "~~/app/lib/api";

export type CompletedPlayback = { relativePath: string; title: string };

/** Omitted fields keep the session's current value; null turns subtitles off or plays at full quality. */
export type IntentChange = {
  subtitleIndex?: number | null;
  maxHeight?: number | null;
  seekPositionSeconds?: number;
};

export interface Playback {
  session: Readonly<Ref<DeepReadonly<PlaybackSession> | null>>;
  /** The last file the TV finished, kept after its session expires so Play Next stays available. */
  completed: Readonly<Ref<DeepReadonly<CompletedPlayback> | null>>;
  loading: Readonly<Ref<boolean>>;
  error: Readonly<Ref<Error | null>>;
  commandError: Readonly<Ref<string | null>>;
  sending: Readonly<Ref<boolean>>;
  pending: ComputedRef<boolean>;
  start: (relativePath: string, startPositionSeconds?: number) => Promise<boolean>;
  /** Subtitle and quality changes are remembered by the server and start the next session too. */
  updateIntent: (desiredState: PlaybackDesiredState, options?: IntentChange) => Promise<boolean>;
}

export const [injectPlayback, providePlayback] = useProvideInject<Playback>("Playback");
