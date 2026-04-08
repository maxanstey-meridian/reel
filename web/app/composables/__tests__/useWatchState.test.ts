// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);

const mockToastAdd = vi.fn();
vi.stubGlobal("useToast", () => ({ add: mockToastAdd }));

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

  it("shows toast after setWatched succeeds", async () => {
    const { setWatched } = useWatchState();
    await setWatched("Films/movie.mp4");

    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Marked as watched",
        icon: "i-lucide-eye",
      }),
    );
  });

  it("shows toast after setUnwatched succeeds", async () => {
    const { setUnwatched } = useWatchState();
    await setUnwatched("Films/movie.mp4");

    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Marked as unwatched",
        icon: "i-lucide-eye-off",
      }),
    );
  });

  it("does not show toast when setWatched fails", async () => {
    mockRivetFetch.mockRejectedValue(new Error("Network error"));

    const { setWatched } = useWatchState();
    await expect(setWatched("Films/movie.mp4")).rejects.toThrow("Network error");

    expect(mockToastAdd).not.toHaveBeenCalled();
  });
});
