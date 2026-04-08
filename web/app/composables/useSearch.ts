import { ref } from "vue";
import { search as rivetSearch } from "~~/generated/rivet/client/reel";
import type { FileNode } from "~~/generated/rivet/types/domain";

export const useSearch = () => {
  const results = ref<FileNode[]>([]);
  const loading = ref(false);
  const error = ref<string | null>(null);
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;

  const search = async (
    q: string,
    tag: string | null = null,
    genre: string | null = null,
    watched: boolean | null = null,
  ) => {
    loading.value = true;
    error.value = null;

    try {
      results.value = await rivetSearch(q || null, tag, genre, watched);
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Search failed";
    } finally {
      loading.value = false;
    }
  };

  const debouncedSearch = (
    q: string,
    tag: string | null = null,
    genre: string | null = null,
    watched: boolean | null = null,
  ) => {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
    }

    if (!q.trim() && !tag && !genre && watched === null) {
      results.value = [];
      loading.value = false;
      error.value = null;
      return;
    }

    debounceTimer = setTimeout(() => {
      search(q, tag, genre, watched);
    }, 300);
  };

  return { results, loading, error, search, debouncedSearch };
};
