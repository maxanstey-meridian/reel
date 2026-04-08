// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";
import type { FileNode } from "~~/generated/rivet/types/domain";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("ref", ref);
vi.stubGlobal("computed", computed);

// Mock Rivet library function
const mockLibrary = vi.fn();

vi.mock("~~/generated/rivet/client/reel", () => ({
  library: (...args: unknown[]) => mockLibrary(...args),
}));

// Import after mocks
const { useLibrary } = await import("../useLibrary");

const fixtureTree: FileNode = {
  name: "root",
  type: "directory",
  relativePath: "",
  extension: null,
  sizeBytes: null,
  children: [
    {
      name: "Films",
      type: "directory",
      relativePath: "Films",
      extension: null,
      sizeBytes: null,
      children: [
        {
          name: "movie.mp4",
          type: "file",
          relativePath: "Films/movie.mp4",
          extension: ".mp4",
          sizeBytes: 1024000,
          children: null,
          parsedTitle: "movie",
          year: null,
          thumbnailUrl: "/api/files/thumbnail?path=Films%2Fmovie.mp4",
          watched: null,
          displayTitle: "movie",
        },
        {
          name: "Sci-Fi",
          type: "directory",
          relativePath: "Films/Sci-Fi",
          extension: null,
          sizeBytes: null,
          children: [
            {
              name: "blade-runner.mkv",
              type: "file",
              relativePath: "Films/Sci-Fi/blade-runner.mkv",
              extension: ".mkv",
              sizeBytes: 5000000,
              children: null,
              parsedTitle: "blade runner",
              year: null,
              thumbnailUrl: "/api/files/thumbnail?path=Films%2FSci-Fi%2Fblade-runner.mkv",
              watched: null,
              displayTitle: "blade runner",
            },
          ],
          parsedTitle: null,
          year: null,
          thumbnailUrl: null,
          watched: null,
          displayTitle: null,
        },
      ],
      parsedTitle: null,
      year: null,
      thumbnailUrl: null,
      watched: null,
      displayTitle: null,
    },
    {
      name: "readme.txt",
      type: "file",
      relativePath: "readme.txt",
      extension: ".txt",
      sizeBytes: 512,
      children: null,
      parsedTitle: "readme",
      year: null,
      thumbnailUrl: "/api/files/thumbnail?path=readme.txt",
      watched: null,
      displayTitle: "readme",
    },
  ],
  parsedTitle: null,
  year: null,
  thumbnailUrl: null,
  watched: null,
  displayTitle: null,
};

describe("useLibrary", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLibrary.mockResolvedValue(fixtureTree);
  });

  it("fetches library tree on init and exposes root children", async () => {
    const { currentChildren, loading } = useLibrary();

    // loading should be true initially
    expect(loading.value).toBe(true);

    // Wait for the async fetch to resolve
    await vi.waitFor(() => {
      expect(loading.value).toBe(false);
    });

    expect(mockLibrary).toHaveBeenCalledOnce();
    expect(currentChildren.value).toHaveLength(2);
    expect(currentChildren.value[0]!.name).toBe("Films");
    expect(currentChildren.value[1]!.name).toBe("readme.txt");
  });

  it("enterDirectory navigates into a child directory", async () => {
    const { currentChildren, enterDirectory, loading } = useLibrary();

    await vi.waitFor(() => {
      expect(loading.value).toBe(false);
    });

    enterDirectory("Films");

    expect(currentChildren.value).toHaveLength(2);
    expect(currentChildren.value[0]!.name).toBe("movie.mp4");
    expect(currentChildren.value[1]!.name).toBe("Sci-Fi");
  });

  it("navigateUp returns to parent directory", async () => {
    const { currentChildren, enterDirectory, navigateUp, loading } = useLibrary();

    await vi.waitFor(() => {
      expect(loading.value).toBe(false);
    });

    enterDirectory("Films");
    expect(currentChildren.value[0]!.name).toBe("movie.mp4");

    navigateUp();
    expect(currentChildren.value).toHaveLength(2);
    expect(currentChildren.value[0]!.name).toBe("Films");
  });

  it("navigateUp at root level is a no-op", async () => {
    const { currentChildren, currentPath, navigateUp, loading } = useLibrary();

    await vi.waitFor(() => {
      expect(loading.value).toBe(false);
    });

    navigateUp();
    expect(currentPath.value).toEqual([]);
    expect(currentChildren.value).toHaveLength(2);
  });

  it("breadcrumbs reflects current navigation path", async () => {
    const { breadcrumbs, enterDirectory, loading } = useLibrary();

    await vi.waitFor(() => {
      expect(loading.value).toBe(false);
    });

    expect(breadcrumbs.value).toEqual([]);

    enterDirectory("Films");
    expect(breadcrumbs.value).toEqual([{ name: "Films", path: ["Films"] }]);

    enterDirectory("Sci-Fi");
    expect(breadcrumbs.value).toEqual([
      { name: "Films", path: ["Films"] },
      { name: "Sci-Fi", path: ["Films", "Sci-Fi"] },
    ]);
  });

  it("currentNode returns correct children at nested path", async () => {
    const { currentChildren, enterDirectory, loading } = useLibrary();

    await vi.waitFor(() => {
      expect(loading.value).toBe(false);
    });

    enterDirectory("Films");
    enterDirectory("Sci-Fi");

    expect(currentChildren.value).toHaveLength(1);
    expect(currentChildren.value[0]!.name).toBe("blade-runner.mkv");
  });

  it("handles fetch error and allows retry", async () => {
    mockLibrary.mockRejectedValueOnce(new Error("Network error"));

    const { error, loading, refresh } = useLibrary();

    await vi.waitFor(() => {
      expect(loading.value).toBe(false);
    });

    expect(error.value).toBe("Network error");

    // Retry
    mockLibrary.mockResolvedValueOnce(fixtureTree);
    await refresh();
    expect(error.value).toBeNull();
  });
});
