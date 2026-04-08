// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);

// Mock Rivet client — only mediaClearWatched still uses the generated function
const mockMediaClearWatched = vi.fn();

vi.mock("~~/generated/rivet/client/reel", () => ({
  mediaClearWatched: (...args: unknown[]) => mockMediaClearWatched(...args),
}));

// Mock rivetFetch — setWatched/setUnwatched use it directly to send path as query
const mockRivetFetch = vi.fn();

vi.mock("~~/generated/rivet/rivet", () => ({
  rivetFetch: (...args: unknown[]) => mockRivetFetch(...args),
}));

// Mock useLibrary
const mockRefresh = vi.fn();
vi.stubGlobal("useLibrary", () => ({ refresh: mockRefresh }));

// Import after mocks
const { useWatchState } = await import("../useWatchState");

describe("useWatchState", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockRivetFetch.mockResolvedValue(undefined);
    mockMediaClearWatched.mockResolvedValue(undefined);
    mockRefresh.mockResolvedValue(undefined);
  });

  it("setWatched sends path as query param via rivetFetch and refreshes library", async () => {
    const { setWatched } = useWatchState();

    await setWatched("Films/movie.mp4");

    expect(mockRivetFetch).toHaveBeenCalledWith(
      "PUT",
      "/api/media/watched",
      { body: { path: "Films/movie.mp4" }, query: { path: "Films/movie.mp4" } },
    );
    expect(mockRefresh).toHaveBeenCalled();
  });

  it("setUnwatched sends path as query param via rivetFetch and refreshes library", async () => {
    const { setUnwatched } = useWatchState();

    await setUnwatched("Films/movie.mp4");

    expect(mockRivetFetch).toHaveBeenCalledWith(
      "PUT",
      "/api/media/unwatched",
      { body: { path: "Films/movie.mp4" }, query: { path: "Films/movie.mp4" } },
    );
    expect(mockRefresh).toHaveBeenCalled();
  });

  it("clearWatchState calls mediaClearWatched with path and refreshes library", async () => {
    const { clearWatchState } = useWatchState();

    await clearWatchState("Films/movie.mp4");

    expect(mockMediaClearWatched).toHaveBeenCalledWith("Films/movie.mp4");
    expect(mockRefresh).toHaveBeenCalled();
  });

  it("loading is true during API call and false after", async () => {
    let resolveApi: () => void;
    mockRivetFetch.mockReturnValue(
      new Promise<void>((resolve) => {
        resolveApi = resolve;
      }),
    );

    const { loading, setWatched } = useWatchState();

    expect(loading.value).toBe(false);

    const promise = setWatched("Films/movie.mp4");

    expect(loading.value).toBe(true);

    resolveApi!();
    await promise;

    expect(loading.value).toBe(false);
  });

  it("loading resets to false even if API call fails", async () => {
    mockRivetFetch.mockRejectedValue(new Error("Network error"));

    const { loading, setWatched } = useWatchState();

    await expect(setWatched("Films/movie.mp4")).rejects.toThrow("Network error");

    expect(loading.value).toBe(false);
  });
});
