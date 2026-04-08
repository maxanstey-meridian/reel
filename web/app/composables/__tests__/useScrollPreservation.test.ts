// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ref, nextTick, watch } from "vue";

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
vi.stubGlobal("watch", watch);
vi.stubGlobal("nextTick", nextTick);

// Mock main element
let mockMain: {
  scrollTop: number;
  addEventListener: ReturnType<typeof vi.fn>;
  removeEventListener: ReturnType<typeof vi.fn>;
  scrollTo: ReturnType<typeof vi.fn>;
};

const { useScrollPreservation } = await import("../useScrollPreservation");

describe("useScrollPreservation", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mountedCallbacks.length = 0;
    unmountedCallbacks.length = 0;

    mockMain = {
      scrollTop: 0,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      scrollTo: vi.fn(),
    };
    vi.spyOn(document, "querySelector").mockReturnValue(mockMain as unknown as HTMLElement);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("attaches scroll listener on mount", () => {
    const loading = ref(true);
    const currentPath = ref<string[]>([]);
    useScrollPreservation(loading, currentPath);

    mountedCallbacks.forEach((cb) => cb());

    expect(mockMain.addEventListener).toHaveBeenCalledWith(
      "scroll",
      expect.any(Function),
      { passive: true },
    );
  });

  it("stores scroll position after debounce", async () => {
    const scrollTopRef = ref(0);
    vi.stubGlobal("useState", <T>(_key: string, _init: () => T) => {
      return scrollTopRef;
    });

    const loading = ref(true);
    const currentPath = ref<string[]>([]);

    const mod = await import("../useScrollPreservation");
    mod.useScrollPreservation(loading, currentPath);

    mountedCallbacks.forEach((cb) => cb());

    const scrollHandler = mockMain.addEventListener.mock.calls[0]![1] as () => void;

    mockMain.scrollTop = 250;
    scrollHandler();

    // Before debounce fires, value unchanged
    expect(scrollTopRef.value).toBe(0);

    // After full debounce (150ms)
    vi.advanceTimersByTime(150);
    expect(scrollTopRef.value).toBe(250);

    vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
  });

  it("debounces rapid scroll events — only last value persisted", async () => {
    const scrollTopRef = ref(0);
    vi.stubGlobal("useState", <T>(_key: string, _init: () => T) => {
      return scrollTopRef;
    });

    const loading = ref(true);
    const currentPath = ref<string[]>([]);

    const mod = await import("../useScrollPreservation");
    mod.useScrollPreservation(loading, currentPath);

    mountedCallbacks.forEach((cb) => cb());

    const scrollHandler = mockMain.addEventListener.mock.calls[0]![1] as () => void;

    mockMain.scrollTop = 100;
    scrollHandler();
    vi.advanceTimersByTime(50);

    mockMain.scrollTop = 200;
    scrollHandler();
    vi.advanceTimersByTime(50);

    mockMain.scrollTop = 300;
    scrollHandler();

    // Only after 150ms from the last call does the timer fire
    vi.advanceTimersByTime(150);
    expect(scrollTopRef.value).toBe(300);

    vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
  });

  it("restores scroll position when loading completes", async () => {
    const loading = ref(true);
    const currentPath = ref<string[]>([]);

    const scrollTopRef = ref(500);
    vi.stubGlobal("useState", <T>(_key: string, _init: () => T) => {
      return scrollTopRef;
    });

    const mod = await import("../useScrollPreservation");
    mod.useScrollPreservation(loading, currentPath);

    mountedCallbacks.forEach((cb) => cb());

    // Trigger loading → false
    loading.value = false;
    // First tick: watch callback fires, calls nextTick internally
    await nextTick();
    // Second tick: the inner nextTick resolves, scrollTop is set
    await nextTick();

    expect(mockMain.scrollTop).toBe(500);

    // Restore original stub
    vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
  });

  it("resets scroll position when currentPath changes", async () => {
    const scrollTopRef = ref(300);
    vi.stubGlobal("useState", <T>(_key: string, _init: () => T) => {
      return scrollTopRef;
    });

    const loading = ref(false);
    const currentPath = ref<string[]>([]);

    const mod = await import("../useScrollPreservation");
    mod.useScrollPreservation(loading, currentPath);

    mountedCallbacks.forEach((cb) => cb());

    // Change currentPath
    currentPath.value = ["Films"];
    await nextTick();

    expect(scrollTopRef.value).toBe(0);
    expect(mockMain.scrollTo).toHaveBeenCalledWith(0, 0);

    // Restore original stub
    vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
  });

  it("removes scroll listener on unmount", () => {
    const loading = ref(true);
    const currentPath = ref<string[]>([]);
    useScrollPreservation(loading, currentPath);

    mountedCallbacks.forEach((cb) => cb());
    unmountedCallbacks.forEach((cb) => cb());

    expect(mockMain.removeEventListener).toHaveBeenCalledWith(
      "scroll",
      expect.any(Function),
    );
  });

  it("clears debounce timer on unmount", () => {
    const loading = ref(true);
    const currentPath = ref<string[]>([]);
    useScrollPreservation(loading, currentPath);

    mountedCallbacks.forEach((cb) => cb());

    const scrollHandler = mockMain.addEventListener.mock.calls[0]![1] as () => void;
    mockMain.scrollTop = 100;
    scrollHandler();

    // Unmount before debounce fires
    unmountedCallbacks.forEach((cb) => cb());
    vi.advanceTimersByTime(150);

    // No errors — timer was cleared
  });
});
