import type { TrickplayMetadata } from "~~/app/lib/api";
import { clampPosition } from "./playback";

export type TrickplayFrame = {
  frame: number;
  sheet: number;
  x: number;
  y: number;
  positionSeconds: number;
};

export const createScrubInteraction = (
  duration: () => number,
  preview: (position: number) => void,
  commit: (position: number) => void,
) => {
  let dragging = false;
  let position = 0;

  return {
    get dragging(): boolean {
      return dragging;
    },
    get position(): number {
      return position;
    },
    move(target: number): void {
      dragging = true;
      position = clampPosition(target, duration());
      preview(position);
    },
    release(target: number): void {
      position = clampPosition(target, duration());
      dragging = false;
      commit(position);
    },
    cancel(): void {
      dragging = false;
    },
  };
};

export const trickplayFrameAt = (
  positionSeconds: number,
  durationSeconds: number,
  metadata: TrickplayMetadata,
): TrickplayFrame | null => {
  const values = [
    metadata.intervalMs,
    metadata.columns,
    metadata.rows,
    metadata.thumbWidth,
    metadata.thumbHeight,
    metadata.totalThumbs,
  ];
  if (values.some((value) => !Number.isFinite(value) || value <= 0)) {
    return null;
  }
  const position = clampPosition(positionSeconds, durationSeconds);
  const intervalSeconds = metadata.intervalMs / 1000;
  const frame = Math.min(
    Math.max(0, metadata.totalThumbs - 1),
    Math.floor(position / intervalSeconds),
  );
  const framesPerSheet = metadata.columns * metadata.rows;
  const frameOnSheet = frame % framesPerSheet;
  return {
    frame,
    sheet: Math.floor(frame / framesPerSheet),
    x: (frameOnSheet % metadata.columns) * metadata.thumbWidth,
    y: Math.floor(frameOnSheet / metadata.columns) * metadata.thumbHeight,
    positionSeconds: position,
  };
};
