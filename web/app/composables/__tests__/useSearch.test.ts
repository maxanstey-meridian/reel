// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);

// Mock Rivet client
const mockSearch = vi.fn();

vi.mock("~~/generated/rivet/client/reel", () => ({
  search: (...args: unknown[]) => mockSearch(...args),
}));

// Import after mocks
const { useSearch } = await import("../useSearch");

describe("useSearch", () => {
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

  it("search calls Rivet search with correct params", async () => {
    mockSearch.mockResolvedValue([fileNode]);

    const { search } = useSearch();
    await search("test query", "action", "thriller", true);

    expect(mockSearch).toHaveBeenCalledWith("test query", "action", "thriller", true);
  });

  it("search populates results with API response", async () => {
    mockSearch.mockResolvedValue([fileNode]);

    const { results, search } = useSearch();
    await search("test");

    expect(results.value).toEqual([fileNode]);
  });

  it("search passes null for empty query string", async () => {
    const { search } = useSearch();
    await search("");

    expect(mockSearch).toHaveBeenCalledWith(null, null, null, null);
  });

  it("loading is true during API call and false after", async () => {
    let resolveApi: (value: unknown[]) => void;
    mockSearch.mockReturnValue(
      new Promise<unknown[]>((resolve) => {
        resolveApi = resolve;
      }),
    );

    const { loading, search } = useSearch();

    expect(loading.value).toBe(false);

    const promise = search("test");

    expect(loading.value).toBe(true);

    resolveApi!([]);
    await promise;

    expect(loading.value).toBe(false);
  });

  it("error is set on API failure and loading resets", async () => {
    mockSearch.mockRejectedValue(new Error("Network error"));

    const { loading, error, search } = useSearch();
    await search("test");

    expect(error.value).toBe("Network error");
    expect(loading.value).toBe(false);
  });

  it("debouncedSearch only triggers one API call within 300ms", async () => {
    mockSearch.mockResolvedValue([]);

    const { debouncedSearch } = useSearch();

    debouncedSearch("first");
    debouncedSearch("second");
    debouncedSearch("third");

    expect(mockSearch).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(300);

    expect(mockSearch).toHaveBeenCalledTimes(1);
    expect(mockSearch).toHaveBeenCalledWith("third", null, null, null);
  });

  it("debouncedSearch clears results immediately for empty query", () => {
    const { results, debouncedSearch } = useSearch();
    results.value = [fileNode];

    debouncedSearch("");

    expect(results.value).toEqual([]);
    expect(mockSearch).not.toHaveBeenCalled();
  });

  it("debouncedSearch cancels previous pending call", async () => {
    mockSearch.mockResolvedValue([]);

    const { debouncedSearch } = useSearch();

    debouncedSearch("first");
    await vi.advanceTimersByTimeAsync(200);

    debouncedSearch("second");
    await vi.advanceTimersByTimeAsync(300);

    expect(mockSearch).toHaveBeenCalledTimes(1);
    expect(mockSearch).toHaveBeenCalledWith("second", null, null, null);
  });
});
