import type { ComputedRef, DeepReadonly, Ref } from "vue";
import { useProvideInject } from "~~/app/composables/useProvideInject";
import type { PlaybackDesiredState, PlaybackSession } from "~~/app/lib/api";

export type CompletedPlayback = { relativePath: string; title: string };

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
  updateIntent: (
    desiredState: PlaybackDesiredState,
    options?: { subtitleIndex?: number | null; seekPositionSeconds?: number },
  ) => Promise<boolean>;
}

export const [injectPlayback, providePlayback] = useProvideInject<Playback>("Playback");
