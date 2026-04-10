// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);

const mockToastAdd = vi.fn();
vi.stubGlobal("useToast", () => ({ add: mockToastAdd }));

// Mock Rivet modules
const mockCast = vi.fn();
const mockCastStop = vi.fn();
const mockCastQueuePlay = vi.fn();
const mockCastQueueGet = vi.fn();

vi.mock("~~/generated/rivet/client/reel", () => ({
  cast: (...args: unknown[]) => mockCast(...args),
  castStop: (...args: unknown[]) => mockCastStop(...args),
  castQueuePlay: (...args: unknown[]) => mockCastQueuePlay(...args),
  castQueueGet: (...args: unknown[]) => mockCastQueueGet(...args),
}));

// Import after mocks are set up
const { useCast } = await import("../useCast");

describe("useCast", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("cast sets currentFile on success", async () => {
    mockCast.mockResolvedValue(undefined);

    const { cast, currentFile } = useCast();
    await cast("Films/test.mp4");

    expect(mockCast).toHaveBeenCalledWith({ relativePath: "Films/test.mp4" });
    expect(currentFile.value).toBe("Films/test.mp4");
  });

  it("cast passes subtitleIndex to rivet client", async () => {
    mockCast.mockResolvedValue(undefined);

    const { cast } = useCast();
    await cast("Films/test.mp4", { subtitleIndex: 2 });

    expect(mockCast).toHaveBeenCalledWith({ relativePath: "Films/test.mp4", subtitleIndex: 2 });
  });

  it("cast without subtitleIndex does not include subtitleIndex key", async () => {
    mockCast.mockResolvedValue(undefined);

    const { cast } = useCast();
    await cast("Films/test.mp4");

    expect(mockCast).toHaveBeenCalledWith({ relativePath: "Films/test.mp4" });
  });

  it("cast sets casting to true during request", async () => {
    let resolveCast: () => void;
    mockCast.mockReturnValue(
      new Promise<void>((resolve) => {
        resolveCast = resolve;
      }),
    );

    const { cast, casting } = useCast();
    const promise = cast("Films/test.mp4");

    expect(casting.value).toBe(true);

    resolveCast!();
    await promise;

    expect(casting.value).toBe(false);
  });

  it("stop clears currentFile", async () => {
    mockCast.mockResolvedValue(undefined);
    mockCastStop.mockResolvedValue(undefined);

    const { cast, stop, currentFile } = useCast();

    await cast("Films/test.mp4");
    expect(currentFile.value).toBe("Films/test.mp4");

    await stop();
    expect(currentFile.value).toBeNull();
    expect(mockCastStop).toHaveBeenCalled();
  });

  it("cast handles error", async () => {
    mockCast.mockRejectedValue(new Error("Network error"));

    const { cast, currentFile, error } = useCast();
    await cast("Films/test.mp4");

    expect(error.value).toBe("Network error");
    expect(currentFile.value).toBeNull();
  });

  it("stop handles error gracefully", async () => {
    mockCastStop.mockRejectedValue(new Error("Stop failed"));

    const { stop, error } = useCast();
    await stop();

    expect(error.value).toBe("Stop failed");
  });

  it("cast shows toast on success", async () => {
    mockCast.mockResolvedValue(undefined);

    const { cast } = useCast();
    await cast("Films/test.mp4");

    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Casting to TV",
        color: "success",
      }),
    );
  });

  it("cast does not show toast on error", async () => {
    mockCast.mockRejectedValue(new Error("fail"));

    const { cast } = useCast();
    await cast("Films/test.mp4");

    expect(mockToastAdd).not.toHaveBeenCalled();
  });

  it("castQueue calls endpoint and sets currentFile to first path", async () => {
    mockCastQueuePlay.mockResolvedValue(undefined);

    const { castQueue, currentFile, queue } = useCast();
    await castQueue(["Films/a.mp4", "Films/b.mp4", "Films/c.mp4"]);

    expect(mockCastQueuePlay).toHaveBeenCalledWith({
      paths: ["Films/a.mp4", "Films/b.mp4", "Films/c.mp4"],
    });
    expect(currentFile.value).toBe("Films/a.mp4");
    expect(queue.value).toEqual(["Films/b.mp4", "Films/c.mp4"]);
  });

  it("castQueue does nothing for empty array", async () => {
    const { castQueue } = useCast();
    await castQueue([]);

    expect(mockCastQueuePlay).not.toHaveBeenCalled();
  });

  it("castQueue handles error", async () => {
    mockCastQueuePlay.mockRejectedValue(new Error("Queue failed"));

    const { castQueue, currentFile, queue, error } = useCast();
    await castQueue(["Films/a.mp4"]);

    expect(error.value).toBe("Queue failed");
    expect(currentFile.value).toBeNull();
    expect(queue.value).toEqual([]);
  });

  it("fetchQueue populates queue state", async () => {
    mockCastQueueGet.mockResolvedValue(["Films/b.mp4", "Films/c.mp4"]);

    const { fetchQueue, queue } = useCast();
    await fetchQueue();

    expect(queue.value).toEqual(["Films/b.mp4", "Films/c.mp4"]);
  });

  it("fetchQueue clears queue on error", async () => {
    mockCastQueueGet.mockRejectedValue(new Error("fail"));

    const { fetchQueue, queue } = useCast();
    await fetchQueue();

    expect(queue.value).toEqual([]);
  });

  it("stop clears queue", async () => {
    mockCastQueuePlay.mockResolvedValue(undefined);
    mockCastStop.mockResolvedValue(undefined);

    const { castQueue, stop, queue } = useCast();
    await castQueue(["Films/a.mp4", "Films/b.mp4"]);
    expect(queue.value).toEqual(["Films/b.mp4"]);

    await stop();
    expect(queue.value).toEqual([]);
  });
});
