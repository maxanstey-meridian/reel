import type { MaybeRefOrGetter } from "vue";
import { injectPlayback } from "~~/app/features/playback/ports/playback";
import { client, unwrap } from "~~/app/lib/api";

/** Subtitles and preview frames for the playing file, and the file that follows a finished one. */
export const useNowPlayingDetails = (
  // Audio has no frames to preview.
  previewable: MaybeRefOrGetter<boolean>,
) => {
  const { session, completed } = injectPlayback();
  const playingPath = computed(() => session.value?.intent.relativePath);
  // The server lists only the subtitles the stream at this quality can carry.
  const maxHeight = computed(() => session.value?.intent.maxHeight);
  const finishedPath = computed(() => completed.value?.relativePath);

  const { data: nextEpisode } = useAsyncData(
    () => `next-episode:${finishedPath.value}`,
    async (_nuxtApp, { signal }) => {
      const path = finishedPath.value;
      return path
        ? unwrap(await client.GET("/api/library/next", { params: { query: { path } }, signal }))
        : null;
    },
  );
  const { data: subtitles, status: subtitlesStatus } = useAsyncData(
    () => `subtitles:${playingPath.value}:${maxHeight.value}`,
    async (_nuxtApp, { signal }) => {
      const path = playingPath.value;
      return path
        ? unwrap(
            await client.GET("/api/files/subtitles", {
              params: { query: { path, maxHeight: maxHeight.value } },
              signal,
            }),
          )
        : [];
    },
    { default: () => [] },
  );
  const { data: trickplay, error: trickplayError } = useAsyncData(
    () => `trickplay:${playingPath.value}:${toValue(previewable)}`,
    async (_nuxtApp, { signal }) => {
      const path = playingPath.value;
      return path && toValue(previewable)
        ? unwrap(await client.GET("/api/files/trickplay", { params: { query: { path } }, signal }))
        : null;
    },
  );
  return { nextEpisode, subtitles, subtitlesStatus, trickplay, trickplayError };
};
