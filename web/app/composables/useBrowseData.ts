import { readBrowseCache, writeBrowseCache } from "~~/app/lib/browse-cache";

// Shared by every caller of the same key; "defer" stops each mounting page from restarting the fetch.
export const useBrowseData = <T>(
  name: string,
  load: (signal: AbortSignal) => Promise<T>,
  fallback: () => T,
) => {
  const asyncData = useAsyncData(name, (_nuxtApp, { signal }) => load(signal), {
    default: () => readBrowseCache<T>(name) ?? fallback(),
    dedupe: "defer",
  });
  // Only data the server has confirmed is saved, never the stand-in shown before or after a load.
  watch(asyncData.data, (data) => {
    if (asyncData.status.value === "success") {
      writeBrowseCache(name, data);
    }
  });
  return asyncData;
};
