import { describe, expect, it } from "vitest";
import { trickplayFrameAt } from "~~/app/features/playback/logic/trickplay";

const metadata = {
  intervalMs: 10_000,
  columns: 5,
  rows: 4,
  thumbWidth: 160,
  thumbHeight: 90,
  totalThumbs: 42,
};

describe("trickplayFrameAt", () => {
  it("maps time to the correct sheet and sprite coordinates", () => {
    expect(trickplayFrameAt(250, 500, metadata)).toEqual({
      sheet: 1,
      x: 0,
      y: 90,
      positionSeconds: 250,
    });
  });

  it("clamps preview time and frame to available media", () => {
    expect(trickplayFrameAt(999, 500, metadata)).toMatchObject({
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
});
