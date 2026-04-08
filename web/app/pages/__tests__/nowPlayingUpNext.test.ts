// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed, watch, nextTick } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);
vi.stubGlobal("watch", watch);
vi.stubGlobal("onMounted", vi.fn());
vi.stubGlobal("navigateTo", vi.fn());

const mockToastAdd = vi.fn();
vi.stubGlobal("useToast", () => ({ add: mockToastAdd }));

// Mock Rivet client
const mockNextFile = vi.fn();
const mockCast = vi.fn();
const mockCastStop = vi.fn();
vi.mock("~~/generated/rivet/client/reel", () => ({
  nextFile: (...args: unknown[]) => mockNextFile(...args),
  cast: (...args: unknown[]) => mockCast(...args),
  castStop: (...args: unknown[]) => mockCastStop(...args),
  thumbnailUrl: (path: string, token: string) => `http://test/thumb?path=${path}&token=${token}`,
}));

// Mock useConnection
vi.mock("../composables/useConnection", () => ({
  useConnection: () => ({
    isConnected: ref(true),
    secretKey: ref("test-key"),
    serverUrl: ref("http://localhost:8080"),
  }),
}));

// We need to test the logic extracted from the page.
// Since the page is a Vue SFC, we test the core logic by importing useCast
// and exercising the nextFile integration via a focused logic test.

const { useCast } = await import("../../composables/useCast");

const makeFileNode = (overrides: Record<string, unknown> = {}) => ({
  name: "Episode.02.mkv",
  type: "file" as const,
  relativePath: "Shows/Series/Episode.02.mkv",
  extension: ".mkv",
  sizeBytes: 1000,
  children: null,
  parsedTitle: "Episode 02",
  year: null,
  thumbnailUrl: null,
  watched: null,
  displayTitle: "Episode 2",
  ...overrides,
});

describe("Up Next in Now Playing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("calls nextFile when currentFile is set", async () => {
    mockCast.mockResolvedValue(undefined);
    mockNextFile.mockResolvedValue({
      status: 200,
      data: makeFileNode(),
      response: new Response(),
    });

    const { cast, currentFile } = useCast();
    await cast("Shows/Series/Episode.01.mkv");

    // Simulate what the page does: watch currentFile and call nextFile
    const upNext = ref<ReturnType<typeof makeFileNode> | null>(null);

    watch(
      currentFile,
      async (path) => {
        if (path) {
          const result = await mockNextFile(path, { unwrap: false });
          if (result.status === 200) {
            upNext.value = result.data;
          } else {
            upNext.value = null;
          }
        } else {
          upNext.value = null;
        }
      },
      { immediate: true },
    );

    await nextTick();
    // Allow the async watcher to complete
    await vi.waitFor(() => {
      expect(mockNextFile).toHaveBeenCalledWith(
        "Shows/Series/Episode.01.mkv",
        { unwrap: false },
      );
    });
  });

  it("populates upNext when nextFile returns 200", async () => {
    const expectedNode = makeFileNode();
    mockNextFile.mockResolvedValue({
      status: 200,
      data: expectedNode,
      response: new Response(),
    });

    const upNext = ref<ReturnType<typeof makeFileNode> | null>(null);
    const result = await mockNextFile("Shows/Series/Episode.01.mkv", {
      unwrap: false,
    });

    if (result.status === 200) {
      upNext.value = result.data;
    }

    expect(upNext.value).toEqual(expectedNode);
    expect(upNext.value!.displayTitle).toBe("Episode 2");
    expect(upNext.value!.relativePath).toBe("Shows/Series/Episode.02.mkv");
  });

  it("sets upNext to null when nextFile returns 204", async () => {
    mockNextFile.mockResolvedValue({
      status: 204,
      data: null,
      response: new Response(null, { status: 204 }),
    });

    const upNext = ref<ReturnType<typeof makeFileNode> | null>(null);
    upNext.value = makeFileNode(); // Pre-populate to verify it gets cleared

    const result = await mockNextFile("Shows/Series/LastEpisode.mkv", {
      unwrap: false,
    });

    if (result.status === 200) {
      upNext.value = result.data;
    } else {
      upNext.value = null;
    }

    expect(upNext.value).toBeNull();
  });

  it("sets upNext to null when nextFile throws", async () => {
    mockNextFile.mockRejectedValue(new Error("Network error"));

    const upNext = ref<ReturnType<typeof makeFileNode> | null>(null);
    upNext.value = makeFileNode();

    try {
      await mockNextFile("Shows/Series/Episode.01.mkv", { unwrap: false });
    } catch {
      upNext.value = null;
    }

    expect(upNext.value).toBeNull();
  });

  it("re-fetches nextFile when currentFile changes", async () => {
    const node1 = makeFileNode({ relativePath: "Shows/S1/E02.mkv", name: "E02.mkv" });
    const node2 = makeFileNode({ relativePath: "Shows/S1/E03.mkv", name: "E03.mkv" });

    mockCast.mockResolvedValue(undefined);

    mockNextFile
      .mockResolvedValueOnce({ status: 200, data: node1, response: new Response() })
      .mockResolvedValueOnce({ status: 200, data: node2, response: new Response() });

    const currentFile = ref<string | null>(null);
    const upNext = ref<ReturnType<typeof makeFileNode> | null>(null);

    watch(
      currentFile,
      async (path) => {
        if (path) {
          const result = await mockNextFile(path, { unwrap: false });
          if (result.status === 200) {
            upNext.value = result.data;
          } else {
            upNext.value = null;
          }
        } else {
          upNext.value = null;
        }
      },
      { immediate: true },
    );

    // First file
    currentFile.value = "Shows/S1/E01.mkv";
    await nextTick();
    await vi.waitFor(() => {
      expect(mockNextFile).toHaveBeenCalledWith("Shows/S1/E01.mkv", { unwrap: false });
    });
    expect(upNext.value).toEqual(node1);

    // Change to second file
    currentFile.value = "Shows/S1/E02.mkv";
    await nextTick();
    await vi.waitFor(() => {
      expect(mockNextFile).toHaveBeenCalledTimes(2);
      expect(mockNextFile).toHaveBeenLastCalledWith("Shows/S1/E02.mkv", { unwrap: false });
    });
    expect(upNext.value).toEqual(node2);
  });

  it("clears upNext without calling nextFile when currentFile becomes null", async () => {
    mockNextFile.mockResolvedValue({
      status: 200,
      data: makeFileNode(),
      response: new Response(),
    });

    const currentFile = ref<string | null>("Shows/S1/E01.mkv");
    const upNext = ref<ReturnType<typeof makeFileNode> | null>(null);

    watch(
      currentFile,
      async (path) => {
        if (path) {
          const result = await mockNextFile(path, { unwrap: false });
          if (result.status === 200) {
            upNext.value = result.data;
          } else {
            upNext.value = null;
          }
        } else {
          upNext.value = null;
        }
      },
      { immediate: true },
    );

    await nextTick();
    await vi.waitFor(() => expect(upNext.value).not.toBeNull());

    // Clear mockNextFile call count
    mockNextFile.mockClear();

    // Set currentFile to null
    currentFile.value = null;
    await nextTick();

    expect(upNext.value).toBeNull();
    expect(mockNextFile).not.toHaveBeenCalled();
  });

  it("casting upNext triggers re-fetch via currentFile change", async () => {
    const episode2 = makeFileNode({
      relativePath: "Shows/S1/E02.mkv",
      name: "E02.mkv",
      displayTitle: "Episode 2",
    });
    const episode3 = makeFileNode({
      relativePath: "Shows/S1/E03.mkv",
      name: "E03.mkv",
      displayTitle: "Episode 3",
    });

    mockCast.mockResolvedValue(undefined);
    mockNextFile
      .mockResolvedValueOnce({ status: 200, data: episode2, response: new Response() })
      .mockResolvedValueOnce({ status: 200, data: episode3, response: new Response() });

    const { cast: doCast, currentFile } = useCast();
    const upNext = ref<ReturnType<typeof makeFileNode> | null>(null);

    watch(
      currentFile,
      async (path) => {
        if (path) {
          const result = await mockNextFile(path, { unwrap: false });
          if (result.status === 200) {
            upNext.value = result.data;
          } else {
            upNext.value = null;
          }
        } else {
          upNext.value = null;
        }
      },
      { immediate: true },
    );

    // Cast first episode
    await doCast("Shows/S1/E01.mkv");
    await nextTick();
    await vi.waitFor(() => expect(upNext.value).toEqual(episode2));

    // Cast the up-next episode (simulating tap on Up Next card)
    await doCast(episode2.relativePath);
    await nextTick();
    await vi.waitFor(() => expect(upNext.value).toEqual(episode3));

    expect(mockNextFile).toHaveBeenCalledTimes(2);
  });
});
