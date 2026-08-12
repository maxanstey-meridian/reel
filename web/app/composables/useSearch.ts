import { ref } from "vue";
import { api, type FileNode } from "~~/app/lib/api";

export const useSearch = () => {
  const results = ref<FileNode[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);
  const completedQuery = ref("");
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;
  let controller: AbortController | null = null;
  let requestId = 0;

  const search = async (q: string) => {
    const id = ++requestId;
    controller?.abort();
    controller = new AbortController();
    loading.value = true;
    error.value = null;

    try {
      const nextResults = await api.search(q, controller.signal);
      if (id !== requestId) {
        return;
      }
      results.value = nextResults;
      completedQuery.value = q.trim();
    } catch (e) {
      if (id !== requestId || (e instanceof DOMException && e.name === "AbortError")) {
        return;
      }
      error.value = e instanceof Error ? e.message : "Search failed";
    } finally {
      if (id === requestId) {
        loading.value = false;
      }
    }
  };

  const debouncedSearch = (q: string) => {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
    }

    if (!q.trim()) {
      requestId++;
      controller?.abort();
      results.value = [];
      completedQuery.value = "";
      loading.value = false;
      error.value = null;
      return;
    }

    debounceTimer = setTimeout(() => {
      search(q);
    }, 300);
  };

  return { results, loading, error, completedQuery, search, debouncedSearch };
};
