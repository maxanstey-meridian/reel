import type { MaybeRefOrGetter } from "vue";
import { client, unwrap } from "~~/app/lib/api";

/** Subtitles and preview frames for the playing file, and the file that follows a finished one. */
export const useNowPlayingDetails = (
  playingPath: MaybeRefOrGetter<string | undefined>,
  // The session's quality cap: a capped (live) stream can't show image-based subtitles, so they aren't listed.
  maxHeight: MaybeRefOrGetter<number | null | undefined>,
  finishedPath: MaybeRefOrGetter<string | undefined>,
  // Audio has no frames to preview.
  previewable: MaybeRefOrGetter<boolean>,
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
    () => `subtitles:${toValue(playingPath)}:${toValue(maxHeight)}`,
    async (_nuxtApp, { signal }) => {
      const path = toValue(playingPath);
      const cap = toValue(maxHeight) ?? undefined;
      return path
        ? unwrap(
            await client.GET("/api/files/subtitles", {
              params: { query: { path, maxHeight: cap } },
              signal,
            }),
          )
        : [];
    },
    { default: () => [] },
  );
  const { data: trickplay, error: trickplayError } = useAsyncData(
    () => `trickplay:${toValue(playingPath)}:${toValue(previewable)}`,
    async (_nuxtApp, { signal }) => {
      const path = toValue(playingPath);
      return path && toValue(previewable)
        ? unwrap(await client.GET("/api/files/trickplay", { params: { query: { path } }, signal }))
        : null;
    },
  );
  return { nextEpisode, subtitles, subtitlesStatus, trickplay, trickplayError };
};
