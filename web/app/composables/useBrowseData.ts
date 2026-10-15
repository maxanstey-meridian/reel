import { readBrowseCache, writeBrowseCache } from "~~/app/lib/browse-cache";

// Shared by every caller of the same key; "defer" stops each mounting page from restarting the fetch.
export const useBrowseData = <T>(
  name: string,
  load: (signal: AbortSignal) => Promise<T>,
  fallback: () => T,
) => {
  const build = useRuntimeConfig().app.buildId;
  const asyncData = useAsyncData(name, (_nuxtApp, { signal }) => load(signal), {
    default: () => readBrowseCache<T>(build, name) ?? fallback(),
    dedupe: "defer",
  });
  // Saved after a successful load, including local edits made since; never the stand-in shown
  // before a load or after a failed one.
  watch(asyncData.data, (data) => {
    if (asyncData.status.value === "success") {
      writeBrowseCache(build, name, data);
    }
  });
  return asyncData;
};
