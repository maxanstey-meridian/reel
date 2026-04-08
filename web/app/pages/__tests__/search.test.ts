// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ref, computed, watch, nextTick } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);
vi.stubGlobal("watch", watch);

// Mock Rivet client
const mockSearch = vi.fn();
vi.mock("~~/generated/rivet/client/reel", () => ({
  search: (...args: unknown[]) => mockSearch(...args),
}));

// Import composable after mocks
const { useSearch } = await import("../../composables/useSearch");

/**
 * Replicate the page's reactive wiring so we can test state transitions
 * without mounting the Vue component.
 */
const setupSearchPage = () => {
  const query = ref("");
  const hasSearched = ref(false);
  const { results, loading, error, debouncedSearch } = useSearch();

  watch(query, (q) => {
    if (q.trim()) {
      hasSearched.value = true;
    }
    debouncedSearch(q);
  });

  const clearSearch = () => {
    query.value = "";
    hasSearched.value = false;
  };

  /**
   * Derive which view state the template would render.
   * Mirrors the v-if chain in search.vue exactly.
   */
  const viewState = (): "loading" | "error" | "results" | "no-results" | "empty" => {
    if (loading.value) return "loading";
    if (error.value) return "error";
    if (results.value.length > 0) return "results";
    if (query.value.trim().length > 0 && hasSearched.value) return "no-results";
    return "empty";
  };

  return { query, hasSearched, results, loading, error, clearSearch, viewState, debouncedSearch };
};

describe("Search page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    mockSearch.mockResolvedValue([]);
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
  });

  describe("regression guards", () => {
    it("empty state and no-results are distinguishable — hasSearched is required", async () => {
      // Both states have results.length === 0. Without hasSearched,
      // they'd be indistinguishable when query is empty after clearing.
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
  });
});
