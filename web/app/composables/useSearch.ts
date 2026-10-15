import { refDebounced } from "@vueuse/core";
import { client, unwrap } from "~~/app/lib/api";

export const useSearch = () => {
  const query = ref("");
  const debounced = refDebounced(query, 300);
  const term = computed(() => debounced.value.trim());
  const {
    data: results,
    status,
    error,
    refresh,
  } = useAsyncData(
    "search",
    async (_nuxtApp, { signal }) =>
      term.value
        ? unwrap(await client.GET("/api/search", { params: { query: { q: term.value } }, signal }))
        : [],
    { default: () => [], watch: [term] },
  );
  // Results are only an answer to the query once the debounced search for it has finished.
  const searching = computed(() => status.value === "pending" || query.value.trim() !== term.value);
  const { rescan, rescanning } = useRescan(() => refresh({ dedupe: "cancel" }));
  return { query, term, results, searching, error, refresh, rescan, rescanning };
};
