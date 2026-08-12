// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

vi.stubGlobal("ref", ref);

const mockSearch = vi.fn();
vi.mock("~~/app/lib/api", () => ({
  api: { search: (...args: unknown[]) => mockSearch(...args) },
}));

const { useSearch } = await import("../useSearch");

describe("useSearch", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    mockSearch.mockResolvedValue([]);
  });

  afterEach(() => vi.useRealTimers());

  it("searches by text without filters", async () => {
    const { search } = useSearch();
    await search("movie");
    expect(mockSearch).toHaveBeenCalledWith("movie", expect.any(AbortSignal));
  });

  it("debounces text searches", async () => {
    const { debouncedSearch } = useSearch();
    debouncedSearch("first");
    debouncedSearch("second");
    await vi.advanceTimersByTimeAsync(300);
    expect(mockSearch).toHaveBeenCalledOnce();
    expect(mockSearch).toHaveBeenCalledWith("second", expect.any(AbortSignal));
  });

  it("clears results for an empty query", () => {
    const { results, debouncedSearch } = useSearch();
    results.value = [{ name: "Movie", type: "file", relativePath: "Movie.mkv" }];
    debouncedSearch("");
    expect(results.value).toEqual([]);
  });

  it("ignores stale responses and keeps loading for the current request", async () => {
    let resolveFirst!: (value: unknown[]) => void;
    let resolveSecond!: (value: unknown[]) => void;
    mockSearch
      .mockReturnValueOnce(new Promise((resolve) => (resolveFirst = resolve)))
      .mockReturnValueOnce(new Promise((resolve) => (resolveSecond = resolve)));
    const { results, loading, completedQuery, search } = useSearch();

    const first = search("first");
    const second = search("second");
    resolveFirst([{ name: "Old", type: "file", relativePath: "old.mkv" }]);
    await first;
    expect(results.value).toEqual([]);
    expect(loading.value).toBe(true);

    resolveSecond([{ name: "New", type: "file", relativePath: "new.mkv" }]);
    await second;
    expect(results.value[0]?.name).toBe("New");
    expect(completedQuery.value).toBe("second");
    expect(loading.value).toBe(false);
  });

  it("ignores a stale request error", async () => {
    let rejectFirst!: (reason: Error) => void;
    mockSearch
      .mockReturnValueOnce(new Promise((_resolve, reject) => (rejectFirst = reject)))
      .mockResolvedValueOnce([]);
    const { error, search } = useSearch();

    const first = search("first");
    await search("second");
    rejectFirst(new Error("stale failure"));
    await first;
    expect(error.value).toBeNull();
  });
});
