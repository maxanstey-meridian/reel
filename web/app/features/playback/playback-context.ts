import { useProvideInject } from "~~/app/composables/useProvideInject";
import type { PlaybackContext } from "./usePlaybackSession";

export const [injectPlayback, providePlayback] = useProvideInject<PlaybackContext>("Playback");
