import type { MaybeRefOrGetter } from "vue";
import { client, unwrap } from "~~/app/lib/api";

/** Subtitles and preview frames for the playing file, and the file that follows a finished one. */
export const useNowPlayingDetails = (
  playingPath: MaybeRefOrGetter<string | undefined>,
  finishedPath: MaybeRefOrGetter<string | undefined>,
) => {
  const { data: nextEpisode } = useAsyncData(
    () => `next-episode:${toValue(finishedPath)}`,
    async (_nuxtApp, { signal }) => {
      const path = toValue(finishedPath);
      return path
        ? unwrap(await client.GET("/api/library/next", { params: { query: { path } }, signal }))
        : null;
    },
  );
  const { data: subtitles, status: subtitlesStatus } = useAsyncData(
    () => `subtitles:${toValue(playingPath)}`,
    async (_nuxtApp, { signal }) => {
      const path = toValue(playingPath);
      return path
        ? unwrap(await client.GET("/api/files/subtitles", { params: { query: { path } }, signal }))
        : [];
    },
    { default: () => [] },
  );
  const { data: trickplay, error: trickplayError } = useAsyncData(
    () => `trickplay:${toValue(playingPath)}`,
    async (_nuxtApp, { signal }) => {
      const path = toValue(playingPath);
      return path
        ? unwrap(await client.GET("/api/files/trickplay", { params: { query: { path } }, signal }))
        : null;
    },
  );
  return { nextEpisode, subtitles, subtitlesStatus, trickplay, trickplayError };
};
