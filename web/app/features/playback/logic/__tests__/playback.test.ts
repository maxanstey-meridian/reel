import { describe, expect, it } from "vitest";
import type { PlaybackObservationState, PlaybackSession } from "~~/app/lib/api";
import {
  clampPosition,
  formatPlaybackTime,
  hasIncompleteProgress,
  isPlaybackPending,
} from "../playback";

const session = (
  appliedRevision: number,
  appliedSeekId: number | null = null,
  observationState: PlaybackObservationState = "paused",
  desiredState: PlaybackSession["intent"]["desiredState"] = "paused",
): PlaybackSession => ({
  sessionId: "6767fab3-64de-47de-ae53-b0e2981646c0",
  revision: 3,
  createdAtUtc: "2026-08-11T00:00:00Z",
  updatedAtUtc: "2026-08-11T00:00:00Z",
  tvConnected: true,
  intent: {
    relativePath: "Films/Test.mp4",
    desiredState,
    pendingSeek: { seekId: 7, positionSeconds: 42, introducedRevision: 3 },
  },
  observation: {
    observationSequence: 1,
    appliedRevision,
    appliedSeekId,
    state: observationState,
    relativePath: "Films/Test.mp4",
    positionSeconds: 42,
    durationSeconds: 100,
    updatedAtUtc: "2026-08-11T00:00:00Z",
  },
});

describe("playback logic", () => {
  it("clamps invalid and out-of-range positions", () => {
    expect(clampPosition(Number.NaN, 100)).toBe(0);
    expect(clampPosition(-1, 100)).toBe(0);
    expect(clampPosition(101, 100)).toBe(100);
  });

  it("formats short and long positions", () => {
    expect(formatPlaybackTime(65)).toBe("1:05");
    expect(formatPlaybackTime(3661)).toBe("1:01:01");
  });

  it("offers Resume only for incomplete persisted progress", () => {
    expect(hasIncompleteProgress({ positionSeconds: 60, durationSeconds: 100 })).toBe(true);
    expect(hasIncompleteProgress({ positionSeconds: 90, durationSeconds: 100 })).toBe(false);
    expect(hasIncompleteProgress({ positionSeconds: 0, durationSeconds: 100 })).toBe(false);
  });

  it("keeps accepted revisions and seeks pending until TV observation applies them", () => {
    expect(isPlaybackPending(session(2, 6))).toBe(true);
    expect(isPlaybackPending(session(3, 6))).toBe(true);
    expect(isPlaybackPending(session(3, 7))).toBe(false);
  });

  it("requires the observed stable state to match the desired state", () => {
    expect(isPlaybackPending(session(3, 7, "playing", "playing"))).toBe(false);
    expect(isPlaybackPending(session(3, 7, "paused", "playing"))).toBe(true);
    expect(isPlaybackPending(session(3, 7, "paused", "paused"))).toBe(false);
    expect(isPlaybackPending(session(3, 7, "idle", "stopped"))).toBe(false);
    expect(isPlaybackPending(session(3, 7, "playing", "stopped"))).toBe(true);
  });

  it("renders applied transitional and terminal TV outcomes instead of calling them pending", () => {
    expect(isPlaybackPending(session(3, 7, "loading", "playing"))).toBe(false);
    expect(isPlaybackPending(session(3, 7, "buffering", "playing"))).toBe(false);
    expect(isPlaybackPending(session(3, 7, "error", "playing"))).toBe(false);
    expect(isPlaybackPending(session(3, 7, "ended", "playing"))).toBe(false);
  });
});
