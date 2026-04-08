// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ref, computed, watch, nextTick } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);
vi.stubGlobal("watch", watch);
vi.stubGlobal("onMounted", (fn: () => void) => fn());

// Mock Rivet client
const mockSearch = vi.fn();
const mockTagsAll = vi.fn();
vi.mock("~~/generated/rivet/client/reel", () => ({
  search: (...args: unknown[]) => mockSearch(...args),
  tagsAll: (...args: unknown[]) => mockTagsAll(...args),
}));

// Import composables after mocks
const { useSearch } = await import("../../composables/useSearch");
const { useTags } = await import("../../composables/useTags");

/**
 * Replicate the page's reactive wiring so we can test state transitions
 * without mounting the Vue component.
 */
const RECENT_SEARCHES_KEY = "reel:recentSearches";
const MAX_RECENT = 5;

const setupSearchPage = () => {
  const query = ref("");
  const hasSearched = ref(false);
  const activeTag = ref<string | null>(null);
  const watchedFilter = ref<boolean | null>(null);
  const recentSearches = ref<string[]>([]);
  const allTags = ref<{ tag: string; count: number }[]>([]);
  const { results, loading, error, debouncedSearch } = useSearch();
  const { getAllTags } = useTags();

  // Mirrors onMounted block in search.vue
  try {
    const stored = localStorage.getItem(RECENT_SEARCHES_KEY);
    if (stored) {
      recentSearches.value = JSON.parse(stored);
    }
  } catch {
    // ignore
  }
  getAllTags().then((tags: { tag: string; count: number }[]) => {
    allTags.value = tags;
  }).catch(() => {});

  const triggerSearch = () => {
    const q = query.value;
    if (q.trim() || activeTag.value || watchedFilter.value !== null) {
      hasSearched.value = true;
    }
    debouncedSearch(q, activeTag.value, null, watchedFilter.value);
  };

  watch(query, triggerSearch);
  watch(activeTag, triggerSearch);
  watch(watchedFilter, triggerSearch);

  watch(results, (newResults) => {
    const trimmed = query.value.trim();
    if (newResults.length > 0 && trimmed) {
      const filtered = recentSearches.value.filter((s) => s !== trimmed);
      recentSearches.value = [trimmed, ...filtered].slice(0, MAX_RECENT);
      localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(recentSearches.value));
    }
  });

  const clearSearch = () => {
    query.value = "";
    hasSearched.value = false;
    activeTag.value = null;
    watchedFilter.value = null;
  };

  const selectRecent = (term: string) => {
    query.value = term;
  };

  const clearHistory = () => {
    recentSearches.value = [];
    localStorage.removeItem(RECENT_SEARCHES_KEY);
  };

  const hasActiveFilters = computed(() => activeTag.value !== null || watchedFilter.value !== null);

  const noResultsMessage = computed(() => {
    const parts: string[] = [];
    if (query.value.trim()) {
      parts.push(`for '${query.value.trim()}'`);
    }
    if (activeTag.value) {
      parts.push(`tagged ${activeTag.value}`);
    }
    if (watchedFilter.value === false) {
      parts.push("that are unwatched");
    } else if (watchedFilter.value === true) {
      parts.push("that are watched");
    }
    if (parts.length === 0) return "No results found";
    return `No results found ${parts.join(" ")}`;
  });

  /**
   * Derive which view state the template would render.
   * Mirrors the v-if chain in search.vue exactly.
   */
  const viewState = (): "loading" | "error" | "results" | "no-results" | "empty" => {
    if (loading.value) return "loading";
    if (error.value) return "error";
    if (results.value.length > 0) return "results";
    if ((query.value.trim().length > 0 || hasActiveFilters.value) && hasSearched.value) return "no-results";
    return "empty";
  };

  return {
    query, hasSearched, results, loading, error, clearSearch, viewState,
    debouncedSearch, activeTag, watchedFilter, hasActiveFilters, noResultsMessage,
    getAllTags, allTags, recentSearches, selectRecent, clearHistory,
  };
};

describe("Search page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    mockSearch.mockResolvedValue([]);
    mockTagsAll.mockResolvedValue([]);
    localStorage.clear();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const fileNode = {
    name: "Movie.mkv",
    type: "file" as const,
    relativePath: "Films/Movie.mkv",
    extension: ".mkv",
    sizeBytes: 1000,
    children: null,
    parsedTitle: "Movie",
    year: 2024,
    thumbnailUrl: null,
    watched: null,
    displayTitle: "Movie",
  };

  describe("hasSearched transitions", () => {
    it("starts false before any query", () => {
      const { hasSearched } = setupSearchPage();
      expect(hasSearched.value).toBe(false);
    });

    it("becomes true when a non-empty query is entered", async () => {
      const { query, hasSearched } = setupSearchPage();

      query.value = "test";
      await nextTick();

      expect(hasSearched.value).toBe(true);
    });

    it("stays true when query changes to another non-empty value", async () => {
      const { query, hasSearched } = setupSearchPage();

      query.value = "first";
      await nextTick();
      query.value = "second";
      await nextTick();

      expect(hasSearched.value).toBe(true);
    });

    it("stays true when query is manually cleared (watch fires with empty string)", async () => {
      const { query, hasSearched } = setupSearchPage();

      query.value = "test";
      await nextTick();
      expect(hasSearched.value).toBe(true);

      // Manually setting query to empty does NOT reset hasSearched —
      // only clearSearch does that. The watch just skips setting it to true.
      query.value = "";
      await nextTick();
      expect(hasSearched.value).toBe(true);
    });

    it("becomes true when a filter is set without a query", async () => {
      const { activeTag, hasSearched } = setupSearchPage();

      activeTag.value = "Action";
      await nextTick();

      expect(hasSearched.value).toBe(true);
    });

    it("becomes true when watched filter is set without a query", async () => {
      const { watchedFilter, hasSearched } = setupSearchPage();

      watchedFilter.value = false;
      await nextTick();

      expect(hasSearched.value).toBe(true);
    });
  });

  describe("clearSearch", () => {
    it("resets query to empty string", async () => {
      const { query, clearSearch } = setupSearchPage();

      query.value = "test";
      await nextTick();

      clearSearch();
      expect(query.value).toBe("");
    });

    it("resets hasSearched to false", async () => {
      const { query, hasSearched, clearSearch } = setupSearchPage();

      query.value = "test";
      await nextTick();
      expect(hasSearched.value).toBe(true);

      clearSearch();
      expect(hasSearched.value).toBe(false);
    });

    it("resets activeTag and watchedFilter", async () => {
      const { activeTag, watchedFilter, clearSearch } = setupSearchPage();

      activeTag.value = "Action";
      watchedFilter.value = true;
      await nextTick();

      clearSearch();
      expect(activeTag.value).toBeNull();
      expect(watchedFilter.value).toBeNull();
    });
  });

  describe("view state derivation", () => {
    it("shows empty state before any search", () => {
      const { viewState } = setupSearchPage();
      expect(viewState()).toBe("empty");
    });

    it("shows empty state after clearSearch", async () => {
      const { query, clearSearch, viewState } = setupSearchPage();

      query.value = "test";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      clearSearch();
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(viewState()).toBe("empty");
    });

    it("shows no-results when query is non-empty and results are empty", async () => {
      mockSearch.mockResolvedValue([]);
      const { query, viewState } = setupSearchPage();

      query.value = "nonexistent";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(viewState()).toBe("no-results");
    });

    it("shows results when search returns matches", async () => {
      mockSearch.mockResolvedValue([fileNode]);
      const { query, viewState } = setupSearchPage();

      query.value = "movie";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(viewState()).toBe("results");
    });

    it("shows error when search fails", async () => {
      mockSearch.mockRejectedValue(new Error("Network error"));
      const { query, viewState } = setupSearchPage();

      query.value = "test";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(viewState()).toBe("error");
    });

    it("shows no-results when only tag filter is active and results are empty", async () => {
      mockSearch.mockResolvedValue([]);
      const { activeTag, viewState } = setupSearchPage();

      activeTag.value = "Action";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(viewState()).toBe("no-results");
    });

    it("shows no-results when only watched filter is active and results are empty", async () => {
      mockSearch.mockResolvedValue([]);
      const { watchedFilter, viewState } = setupSearchPage();

      watchedFilter.value = false;
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(viewState()).toBe("no-results");
    });
  });

  describe("filter wiring", () => {
    it("loads all tags via getAllTags on mount", async () => {
      const mockTags = [
        { tag: "Action", count: 12 },
        { tag: "Comedy", count: 5 },
      ];
      mockTagsAll.mockResolvedValue(mockTags);

      const { allTags } = setupSearchPage();

      // getAllTags is async — flush the microtask
      await vi.waitFor(() => {
        expect(allTags.value).toEqual(mockTags);
      });
      expect(mockTagsAll).toHaveBeenCalledTimes(1);
    });

    it("tag filter passes through to debouncedSearch", async () => {
      const { query, activeTag } = setupSearchPage();

      query.value = "test";
      await nextTick();
      activeTag.value = "Action";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(mockSearch).toHaveBeenCalledWith("test", "Action", null, null);
    });

    it("watched filter passes through to debouncedSearch", async () => {
      const { query, watchedFilter } = setupSearchPage();

      query.value = "test";
      await nextTick();
      watchedFilter.value = false;
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(mockSearch).toHaveBeenCalledWith("test", null, null, false);
    });

    it("all filters combine with text query", async () => {
      const { query, activeTag, watchedFilter } = setupSearchPage();

      query.value = "movie";
      await nextTick();
      activeTag.value = "Action";
      await nextTick();
      watchedFilter.value = true;
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(mockSearch).toHaveBeenCalledWith("movie", "Action", null, true);
    });

    it("filter-only search works without text query", async () => {
      mockSearch.mockResolvedValue([fileNode]);
      const { activeTag, viewState } = setupSearchPage();

      activeTag.value = "Action";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(mockSearch).toHaveBeenCalledWith(null, "Action", null, null);
      expect(viewState()).toBe("results");
    });

    it("watched filter cycles All → Unwatched → Watched → All", () => {
      const { watchedFilter } = setupSearchPage();

      // Simulate the cycling logic from the component
      const cycle = () => {
        if (watchedFilter.value === null) {
          watchedFilter.value = false;
        } else if (watchedFilter.value === false) {
          watchedFilter.value = true;
        } else {
          watchedFilter.value = null;
        }
      };

      expect(watchedFilter.value).toBeNull(); // All
      cycle();
      expect(watchedFilter.value).toBe(false); // Unwatched
      cycle();
      expect(watchedFilter.value).toBe(true); // Watched
      cycle();
      expect(watchedFilter.value).toBeNull(); // All again
    });

    it("clearing active tag removes it from search params", async () => {
      const { query, activeTag } = setupSearchPage();

      query.value = "test";
      await nextTick();
      activeTag.value = "Action";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      mockSearch.mockClear();
      activeTag.value = null;
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(mockSearch).toHaveBeenCalledWith("test", null, null, null);
    });
  });

  describe("no-results message", () => {
    it("reflects active tag filter", async () => {
      mockSearch.mockResolvedValue([]);
      const { activeTag, noResultsMessage } = setupSearchPage();

      activeTag.value = "Action";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(noResultsMessage.value).toContain("tagged Action");
    });

    it("reflects unwatched filter", async () => {
      mockSearch.mockResolvedValue([]);
      const { watchedFilter, noResultsMessage } = setupSearchPage();

      watchedFilter.value = false;
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(noResultsMessage.value).toContain("unwatched");
    });

    it("reflects watched filter", async () => {
      mockSearch.mockResolvedValue([]);
      const { watchedFilter, noResultsMessage } = setupSearchPage();

      watchedFilter.value = true;
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(noResultsMessage.value).toContain("watched");
    });

    it("reflects query and tag together", async () => {
      mockSearch.mockResolvedValue([]);
      const { query, activeTag, noResultsMessage } = setupSearchPage();

      query.value = "hero";
      await nextTick();
      activeTag.value = "Action";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(noResultsMessage.value).toContain("for 'hero'");
      expect(noResultsMessage.value).toContain("tagged Action");
    });

    it("shows generic message with no filters", () => {
      const { noResultsMessage } = setupSearchPage();
      expect(noResultsMessage.value).toBe("No results found");
    });
  });

  describe("regression guards", () => {
    it("empty state and no-results are distinguishable — hasSearched is required", async () => {
      mockSearch.mockResolvedValue([]);
      const { query, hasSearched, viewState } = setupSearchPage();

      // Before searching: empty state
      expect(viewState()).toBe("empty");

      // After searching with results empty: no-results
      query.value = "nothing";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);
      expect(viewState()).toBe("no-results");

      // hasSearched is what makes the difference
      expect(hasSearched.value).toBe(true);
    });

    it("clearSearch returns to inviting empty state, not no-results", async () => {
      mockSearch.mockResolvedValue([]);
      const { query, clearSearch, viewState } = setupSearchPage();

      query.value = "test";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);
      expect(viewState()).toBe("no-results");

      clearSearch();
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      // Must be "empty", not "no-results"
      expect(viewState()).toBe("empty");
    });

    it("clearSearch after filter-only search returns to empty state", async () => {
      mockSearch.mockResolvedValue([]);
      const { activeTag, clearSearch, viewState } = setupSearchPage();

      activeTag.value = "Action";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);
      expect(viewState()).toBe("no-results");

      clearSearch();
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(viewState()).toBe("empty");
    });
  });

  describe("recent searches", () => {
    it("saves query after successful search with results", async () => {
      mockSearch.mockResolvedValue([fileNode]);
      const { query, recentSearches } = setupSearchPage();

      query.value = "movie";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(recentSearches.value).toEqual(["movie"]);
      expect(JSON.parse(localStorage.getItem(RECENT_SEARCHES_KEY)!)).toEqual(["movie"]);
    });

    it("does not save when results are empty", async () => {
      mockSearch.mockResolvedValue([]);
      const { query, recentSearches } = setupSearchPage();

      query.value = "nothing";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(recentSearches.value).toEqual([]);
      expect(localStorage.getItem(RECENT_SEARCHES_KEY)).toBeNull();
    });

    it("does not save for filter-only search without text query", async () => {
      mockSearch.mockResolvedValue([fileNode]);
      const { activeTag, recentSearches } = setupSearchPage();

      activeTag.value = "Action";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(recentSearches.value).toEqual([]);
    });

    it("deduplicates — searching same term moves it to front", async () => {
      // Return new array each call so Vue watch detects the ref change
      mockSearch.mockImplementation(() => Promise.resolve([{ ...fileNode }]));
      const { query, recentSearches } = setupSearchPage();

      query.value = "first";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      query.value = "second";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      query.value = "first";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(recentSearches.value).toEqual(["first", "second"]);
    });

    it("caps at 5 entries", async () => {
      // Return new array each call so Vue watch detects the ref change
      mockSearch.mockImplementation(() => Promise.resolve([{ ...fileNode }]));
      const { query, recentSearches } = setupSearchPage();

      for (const term of ["a", "b", "c", "d", "e", "f"]) {
        query.value = term;
        await nextTick();
        await vi.advanceTimersByTimeAsync(300);
      }

      expect(recentSearches.value).toHaveLength(5);
      expect(recentSearches.value).toEqual(["f", "e", "d", "c", "b"]);
    });

    it("tapping a recent chip sets query and triggers search", async () => {
      localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(["old query"]));
      const { query, recentSearches, selectRecent } = setupSearchPage();

      expect(recentSearches.value).toEqual(["old query"]);

      selectRecent("old query");
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);

      expect(query.value).toBe("old query");
      expect(mockSearch).toHaveBeenCalledWith("old query", null, null, null);
    });

    it("clearHistory empties list and removes from localStorage", async () => {
      mockSearch.mockResolvedValue([fileNode]);
      const { query, recentSearches, clearHistory } = setupSearchPage();

      query.value = "test";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);
      expect(recentSearches.value).toEqual(["test"]);

      clearHistory();
      expect(recentSearches.value).toEqual([]);
      expect(localStorage.getItem(RECENT_SEARCHES_KEY)).toBeNull();
    });

    it("loads recent searches from localStorage on setup", () => {
      localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(["saved1", "saved2"]));
      const { recentSearches } = setupSearchPage();

      expect(recentSearches.value).toEqual(["saved1", "saved2"]);
    });

    it("clearSearch does not clear recent searches", async () => {
      mockSearch.mockResolvedValue([fileNode]);
      const { query, recentSearches, clearSearch } = setupSearchPage();

      query.value = "test";
      await nextTick();
      await vi.advanceTimersByTimeAsync(300);
      expect(recentSearches.value).toEqual(["test"]);

      clearSearch();
      await nextTick();
      expect(recentSearches.value).toEqual(["test"]);
    });
  });
});
