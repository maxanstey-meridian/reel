// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ref } from "vue";

// Capture lifecycle callbacks
const mountedCallbacks: Array<() => void> = [];
const unmountedCallbacks: Array<() => void> = [];

vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("onMounted", (cb: () => void) => {
  mountedCallbacks.push(cb);
});
vi.stubGlobal("onUnmounted", (cb: () => void) => {
  unmountedCallbacks.push(cb);
});

// Mock main element
let mockMain: {
  scrollTop: number;
  addEventListener: ReturnType<typeof vi.fn>;
  removeEventListener: ReturnType<typeof vi.fn>;
};

const { usePullToRefresh } = await import("../usePullToRefresh");

type EventHandler = (e: Partial<TouchEvent>) => void;

const getHandler = (event: string): EventHandler => {
  const call = mockMain.addEventListener.mock.calls.find(
    (c: unknown[]) => c[0] === event,
  );
  return call![1] as EventHandler;
};

const makeTouchEvent = (clientY: number): Partial<TouchEvent> => ({
  touches: [{ clientY } as Touch],
  preventDefault: vi.fn(),
});

describe("usePullToRefresh", () => {
  beforeEach(() => {
    mountedCallbacks.length = 0;
    unmountedCallbacks.length = 0;

    mockMain = {
      scrollTop: 0,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    };
    vi.spyOn(document, "querySelector").mockReturnValue(mockMain as unknown as HTMLElement);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("attaches touch listeners on mount", () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());

    const events = mockMain.addEventListener.mock.calls.map((c: unknown[]) => c[0]);
    expect(events).toContain("touchstart");
    expect(events).toContain("touchmove");
    expect(events).toContain("touchend");
  });

  it("touchmove uses passive: false to allow preventDefault", () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());

    const touchMoveCall = mockMain.addEventListener.mock.calls.find(
      (c: unknown[]) => c[0] === "touchmove",
    );
    expect(touchMoveCall![2]).toEqual({ passive: false });
  });

  it("triggers onRefresh when pulled > 60px", async () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    const { refreshing, pullOffset } = usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());

    const touchStart = getHandler("touchstart");
    const touchMove = getHandler("touchmove");
    const touchEnd = getHandler("touchend");

    touchStart(makeTouchEvent(100));
    touchMove(makeTouchEvent(170));
    expect(pullOffset.value).toBe(70);

    const endPromise = touchEnd({});
    expect(refreshing.value).toBe(true);
    expect(pullOffset.value).toBe(0);

    await endPromise;
    expect(refreshing.value).toBe(false);
    expect(onRefresh).toHaveBeenCalledOnce();
  });

  it("does not trigger onRefresh when pulled < 60px", async () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    const { pullOffset } = usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());

    const touchStart = getHandler("touchstart");
    const touchMove = getHandler("touchmove");
    const touchEnd = getHandler("touchend");

    touchStart(makeTouchEvent(100));
    touchMove(makeTouchEvent(140));
    expect(pullOffset.value).toBe(40);

    await touchEnd({});
    expect(pullOffset.value).toBe(0);
    expect(onRefresh).not.toHaveBeenCalled();
  });

  it("ignores pull when scrollTop > 0", async () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    const { pullOffset } = usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());

    mockMain.scrollTop = 100;

    const touchStart = getHandler("touchstart");
    const touchMove = getHandler("touchmove");
    const touchEnd = getHandler("touchend");

    touchStart(makeTouchEvent(100));
    touchMove(makeTouchEvent(200));
    expect(pullOffset.value).toBe(0);

    await touchEnd({});
    expect(onRefresh).not.toHaveBeenCalled();
  });

  it("cancels pull if scrollTop becomes > 0 during touchmove", async () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    const { pullOffset } = usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());

    const touchStart = getHandler("touchstart");
    const touchMove = getHandler("touchmove");
    const touchEnd = getHandler("touchend");

    touchStart(makeTouchEvent(100));
    touchMove(makeTouchEvent(130));
    expect(pullOffset.value).toBe(30);

    // Simulate scroll happening during touch
    mockMain.scrollTop = 10;
    touchMove(makeTouchEvent(200));
    expect(pullOffset.value).toBe(0);

    await touchEnd({});
    expect(onRefresh).not.toHaveBeenCalled();
  });

  it("calls preventDefault on touchmove when pulling down", () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());

    const touchStart = getHandler("touchstart");
    const touchMove = getHandler("touchmove");

    touchStart(makeTouchEvent(100));

    const moveEvent = makeTouchEvent(150);
    touchMove(moveEvent);

    expect(moveEvent.preventDefault).toHaveBeenCalled();
  });

  it("caps pullOffset at 100px", () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    const { pullOffset } = usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());

    const touchStart = getHandler("touchstart");
    const touchMove = getHandler("touchmove");

    touchStart(makeTouchEvent(100));
    touchMove(makeTouchEvent(350));

    expect(pullOffset.value).toBe(100);
  });

  it("refreshing stays false and resets on onRefresh error", async () => {
    const onRefresh = vi.fn().mockRejectedValue(new Error("fail"));
    const { refreshing, pullOffset } = usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());

    const touchStart = getHandler("touchstart");
    const touchMove = getHandler("touchmove");
    const touchEnd = getHandler("touchend");

    touchStart(makeTouchEvent(100));
    touchMove(makeTouchEvent(170));

    await touchEnd({});
    expect(refreshing.value).toBe(false);
    expect(pullOffset.value).toBe(0);
  });

  it("removes all listeners on unmount", () => {
    const onRefresh = vi.fn().mockResolvedValue(undefined);
    usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());
    unmountedCallbacks.forEach((cb) => cb());

    const removedEvents = mockMain.removeEventListener.mock.calls.map(
      (c: unknown[]) => c[0],
    );
    expect(removedEvents).toContain("touchstart");
    expect(removedEvents).toContain("touchmove");
    expect(removedEvents).toContain("touchend");
  });

  it("ignores new pull gestures while already refreshing", async () => {
    let resolveRefresh: () => void;
    const onRefresh = vi.fn().mockReturnValue(
      new Promise<void>((resolve) => {
        resolveRefresh = resolve;
      }),
    );

    const { refreshing, pullOffset } = usePullToRefresh(onRefresh);

    mountedCallbacks.forEach((cb) => cb());

    const touchStart = getHandler("touchstart");
    const touchMove = getHandler("touchmove");
    const touchEnd = getHandler("touchend");

    // First pull triggers refresh
    touchStart(makeTouchEvent(100));
    touchMove(makeTouchEvent(170));
    const firstEnd = touchEnd({});
    expect(refreshing.value).toBe(true);

    // Second pull during refresh is ignored
    touchStart(makeTouchEvent(100));
    touchMove(makeTouchEvent(200));
    expect(pullOffset.value).toBe(0); // touchstart bails early

    resolveRefresh!();
    await firstEnd;

    expect(refreshing.value).toBe(false);
    expect(onRefresh).toHaveBeenCalledTimes(1);
  });
});
