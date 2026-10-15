import { mountSuspended } from "@nuxt/test-utils/runtime";
import { afterEach, describe, expect, it, vi } from "vitest";
import PullToRefresh from "~~/app/components/PullToRefresh.vue";

const touch = (type: string, clientY?: number): void => {
  const event = new Event(type, { cancelable: true, bubbles: true });
  Object.defineProperty(event, "touches", { value: clientY === undefined ? [] : [{ clientY }] });
  document.body.dispatchEvent(event);
};

const pull = (distance: number): void => {
  touch("touchstart", 100);
  touch("touchmove", 100 + distance);
  touch("touchend");
};

describe("PullToRefresh", () => {
  afterEach(() => window.scrollTo(0, 0));

  it("refreshes once after a long pull from the top", async () => {
    let finish!: () => void;
    const refresh = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          finish = resolve;
        }),
    );
    await mountSuspended(PullToRefresh, { props: { refresh } });

    pull(70);
    pull(70);
    finish();

    await vi.waitFor(() => expect(refresh).toHaveBeenCalledOnce());
  });

  it("ignores short pulls and pulls while the page is scrolled down", async () => {
    const refresh = vi.fn().mockResolvedValue(undefined);
    await mountSuspended(PullToRefresh, { props: { refresh } });

    pull(40);
    window.scrollTo(0, 200);
    pull(200);

    expect(refresh).not.toHaveBeenCalled();
  });

  it("doesn't refresh on a tap after the system cancelled a pull", async () => {
    const refresh = vi.fn().mockResolvedValue(undefined);
    await mountSuspended(PullToRefresh, { props: { refresh } });

    touch("touchstart", 100);
    touch("touchmove", 170);
    touch("touchcancel");
    touch("touchstart", 100);
    touch("touchend");

    expect(refresh).not.toHaveBeenCalled();
  });

  it("stops listening once unmounted", async () => {
    const refresh = vi.fn().mockResolvedValue(undefined);
    const wrapper = await mountSuspended(PullToRefresh, { props: { refresh } });
    wrapper.unmount();

    pull(70);

    expect(refresh).not.toHaveBeenCalled();
  });
});
