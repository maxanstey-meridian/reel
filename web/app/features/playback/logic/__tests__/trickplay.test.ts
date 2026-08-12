import { describe, expect, it, vi } from "vitest";
import { createScrubInteraction, trickplayFrameAt } from "../trickplay";

const metadata = {
  intervalMs: 10_000,
  columns: 5,
  rows: 4,
  thumbWidth: 160,
  thumbHeight: 90,
  totalThumbs: 42,
  spritePath: ".reel/trickplay/test",
};

describe("trickplayFrameAt", () => {
  it("maps time to the correct sheet and sprite coordinates", () => {
    expect(trickplayFrameAt(250, 500, metadata)).toEqual({
      frame: 25,
      sheet: 1,
      x: 0,
      y: 90,
      positionSeconds: 250,
    });
  });

  it("clamps preview time and frame to available media", () => {
    expect(trickplayFrameAt(999, 500, metadata)).toMatchObject({
      frame: 41,
      sheet: 2,
      x: 160,
      y: 0,
      positionSeconds: 500,
    });
  });

  it.each([{ intervalMs: 0 }, { intervalMs: -1 }, { columns: 0 }, { rows: 0 }, { totalThumbs: 0 }])(
    "returns unavailable for invalid metadata: %o",
    (invalid) => {
      expect(trickplayFrameAt(10, 100, { ...metadata, ...invalid })).toBeNull();
    },
  );

  it("previews every drag movement and commits one seek only on release", () => {
    const preview = vi.fn();
    const commit = vi.fn();
    const scrub = createScrubInteraction(() => 100, preview, commit);

    scrub.move(40);
    scrub.move(45);
    expect(preview).toHaveBeenCalledTimes(2);
    expect(commit).not.toHaveBeenCalled();

    scrub.release(45);
    expect(commit).toHaveBeenCalledOnce();
    expect(commit).toHaveBeenCalledWith(45);
  });
});
