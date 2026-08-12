// @vitest-environment jsdom

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createApp, h, nextTick } from "vue";

const playbackGet = vi.fn();
const playbackStart = vi.fn();
const playbackUpdateIntent = vi.fn();

vi.mock("~~/app/lib/api", () => ({
  api: {
    playbackGet: (...args: unknown[]) => playbackGet(...args),
    playbackStart: (...args: unknown[]) => playbackStart(...args),
    playbackUpdateIntent: (...args: unknown[]) => playbackUpdateIntent(...args),
  },
}));

const { usePlaybackSession } = await import("../usePlaybackSession");

const deferred = <T>() => {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
};

const activeSession = {
  sessionId: "6767fab3-64de-47de-ae53-b0e2981646c0",
  revision: 1,
  createdAtUtc: "2026-08-11T00:00:00Z",
  updatedAtUtc: "2026-08-11T00:00:00Z",
  tvConnected: true,
  intent: { relativePath: "Films/Test.mp4", desiredState: "playing" as const },
  observation: null,
};

let unmount: (() => void) | undefined;

const mountComposable = () => {
  let playback: ReturnType<typeof usePlaybackSession> | undefined;
  const root = document.createElement("div");
  const app = createApp({
    setup() {
      playback = usePlaybackSession();
      return () => h("div");
    },
  });
  app.mount(root);
  unmount = () => app.unmount();
  return () => playback!;
};

describe("usePlaybackSession", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
    playbackGet.mockResolvedValue({ session: null });
  });

  afterEach(() => {
    unmount?.();
    unmount = undefined;
    vi.useRealTimers();
  });

  it("restores a server session on initial mount", async () => {
    playbackGet.mockResolvedValue({ session: activeSession });
    const current = mountComposable();
    await nextTick();
    await vi.runOnlyPendingTimersAsync();

    expect(playbackGet).toHaveBeenCalled();
    expect(current().session.value).toEqual(activeSession);
    expect(current().pending.value).toBe(true);
  });

  it("stores accepted intent without claiming it was observed", async () => {
    playbackStart.mockResolvedValue(activeSession);
    const current = mountComposable();

    await current().start("Films/Test.mp4", 42);

    expect(playbackStart).toHaveBeenCalledWith({
      relativePath: "Films/Test.mp4",
      startPositionSeconds: 42,
    });
    expect(current().pending.value).toBe(true);
  });

  it("surfaces command failures and preserves the observed session", async () => {
    playbackGet.mockResolvedValue({ session: activeSession });
    const current = mountComposable();
    await nextTick();
    await vi.runOnlyPendingTimersAsync();
    playbackUpdateIntent.mockRejectedValue(new Error("TV command rejected"));

    await expect(current().updateIntent("paused")).rejects.toThrow("TV command rejected");
    expect(current().commandError.value).toBe("TV command rejected");
    expect(current().session.value).toEqual(activeSession);
  });

  it("preserves an explicit null subtitle selection", async () => {
    playbackGet.mockResolvedValue({
      session: { ...activeSession, intent: { ...activeSession.intent, subtitleIndex: 2 } },
    });
    playbackUpdateIntent.mockResolvedValue(activeSession);
    const current = mountComposable();
    await nextTick();
    await vi.runOnlyPendingTimersAsync();

    await current().updateIntent("playing", { subtitleIndex: null });

    expect(playbackUpdateIntent).toHaveBeenCalledWith(
      expect.objectContaining({ subtitleIndex: null }),
    );
  });

  it("does not let an older poll overwrite a newer accepted command", async () => {
    const poll = deferred<{ session: null }>();
    playbackGet.mockReturnValue(poll.promise);
    playbackStart.mockResolvedValue(activeSession);
    const current = mountComposable();
    await nextTick();

    await current().start("Films/Test.mp4");
    poll.resolve({ session: null });
    await poll.promise;
    await nextTick();

    expect(current().session.value).toEqual(activeSession);
  });

  it("coalesces overlapping polls and runs commands without waiting for them", async () => {
    const poll = deferred<{ session: null }>();
    const command = deferred<typeof activeSession>();
    playbackGet.mockReturnValue(poll.promise);
    playbackStart.mockReturnValue(command.promise);
    const current = mountComposable();
    await nextTick();

    document.dispatchEvent(new Event("visibilitychange"));
    expect(playbackGet).toHaveBeenCalledOnce();

    const commandResult = current().start("Films/Test.mp4");
    await Promise.resolve();
    expect(playbackStart).toHaveBeenCalledOnce();
    expect(current().sending.value).toBe(true);
    expect(current().pending.value).toBe(true);

    command.resolve(activeSession);
    await commandResult;
    expect(current().sending.value).toBe(false);
    poll.resolve({ session: null });
  });

  it("serializes overlapping commands", async () => {
    const first = deferred<typeof activeSession>();
    playbackStart.mockReturnValueOnce(first.promise).mockResolvedValueOnce(activeSession);
    const current = mountComposable();

    const firstResult = current().start("Films/First.mp4");
    const secondResult = current().start("Films/Second.mp4");
    await Promise.resolve();
    expect(playbackStart).toHaveBeenCalledOnce();

    first.resolve(activeSession);
    await firstResult;
    await secondResult;
    expect(playbackStart).toHaveBeenCalledTimes(2);
  });
});
