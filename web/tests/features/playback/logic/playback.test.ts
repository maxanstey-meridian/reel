import { describe, expect, it } from "vitest";
import {
  clampPosition,
  continueWatching,
  describeTvState,
  formatDuration,
  formatPlaybackTime,
  hasIncompleteProgress,
  isPlaybackPending,
  progressFraction,
  shownSubtitleIndex,
  type TvState,
} from "~~/app/features/playback/logic/playback";
import type { PlaybackSession } from "~~/app/lib/api";
import { fileNode, intent, observation } from "../../../support/fixtures";

type SeekRequest = NonNullable<PlaybackSession["intent"]["pendingSeek"]>;

const session = (
  appliedRevision: number,
  observationState: TvState = "paused",
  desiredState: PlaybackSession["intent"]["desiredState"] = "paused",
  pendingSeek: SeekRequest | null = null,
): PlaybackSession => ({
  sessionId: "6767fab3-64de-47de-ae53-b0e2981646c0",
  revision: 3,
  tvConnected: true,
  intent: intent({ relativePath: "Films/Test.mp4", title: "Test", desiredState, pendingSeek }),
  observation: observation({
    observationSequence: 1,
    appliedRevision,
    appliedSeekId: 6,
    state: observationState,
    relativePath: "Films/Test.mp4",
    positionSeconds: 42,
    durationSeconds: 100,
    updatedAtUtc: "2026-08-11T00:00:00Z",
  }),
});

describe("playback logic", () => {
  it("shows a requested subtitle until the TV applies it, then what the TV shows", () => {
    const requested = (appliedRevision: number, shown: number | null): PlaybackSession => {
      const base = session(appliedRevision);
      return {
        ...base,
        intent: { ...base.intent, subtitleIndex: 2 },
        observation: { ...base.observation!, subtitleIndex: shown },
      };
    };

    expect(shownSubtitleIndex(requested(2, null))).toBe(2);
    expect(shownSubtitleIndex(requested(3, 2))).toBe(2);
    expect(shownSubtitleIndex(requested(3, null))).toBeNull();
  });

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
    expect(hasIncompleteProgress({ positionSeconds: 94, durationSeconds: 100 })).toBe(true);
    expect(hasIncompleteProgress({ positionSeconds: 95, durationSeconds: 100 })).toBe(false);
    expect(hasIncompleteProgress({ positionSeconds: 0, durationSeconds: 100 })).toBe(false);
    expect(progressFraction({ positionSeconds: 150, durationSeconds: 100 })).toBe(1);
    expect(progressFraction({ positionSeconds: 10, durationSeconds: 0 })).toBe(0);
  });

  it("keeps accepted revisions and seeks pending until TV observation applies them", () => {
    const seek = { seekId: 7, positionSeconds: 42 };
    expect(isPlaybackPending(session(2))).toBe(true);
    // The server holds a seek until the TV reports applying it, then clears it.
    expect(isPlaybackPending(session(3, "paused", "paused", seek))).toBe(true);
    expect(isPlaybackPending(session(3))).toBe(false);
  });

  it("requires the observed stable state to match the desired state", () => {
    expect(isPlaybackPending(session(3, "playing", "playing"))).toBe(false);
    expect(isPlaybackPending(session(3, "paused", "playing"))).toBe(true);
    expect(isPlaybackPending(session(3, "paused", "paused"))).toBe(false);
    expect(isPlaybackPending(session(3, "idle", "stopped"))).toBe(false);
    expect(isPlaybackPending(session(3, "playing", "stopped"))).toBe(true);
  });

  it("describes what the TV reported in words, and waits while a control is pending", () => {
    expect(describeTvState(session(3, "playing", "playing"), false)).toBe("Playing on the TV");
    expect(describeTvState(session(3, "idle", "stopped"), false)).toBe("Stopped");
    expect(describeTvState(session(3, "error", "playing"), false)).toBe("Couldn't play on the TV");
    expect(describeTvState(session(3, "playing", "paused"), true)).toBe("Waiting for the TV…");
    expect(describeTvState({ tvConnected: false, observation: null }, false)).toBe(
      "Waiting for the TV…",
    );
    expect(describeTvState({ ...session(3), tvConnected: false }, false)).toBe("TV disconnected");
  });

  it("formats a running time in hours and minutes", () => {
    expect(formatDuration(59)).toBe("<1m");
    expect(formatDuration(25 * 60)).toBe("25m");
    expect(formatDuration(2 * 3600 + 5 * 60)).toBe("2h 5m");
  });

  it("continues unfinished files, by path when the library doesn't have them", () => {
    const tree = fileNode({
      name: "",
      type: "directory",
      relativePath: "",
      children: [
        fileNode({ name: "a.mkv", type: "file", relativePath: "Films/a.mkv", parsedTitle: "A" }),
      ],
    });
    const entry = (relativePath: string, positionSeconds: number) => ({
      relativePath,
      positionSeconds,
      durationSeconds: 100,
    });

    const items = continueWatching(
      [entry("Films/a.mkv", 50), entry("Films/done.mkv", 99), entry("Moved/b.mkv", 25)],
      tree,
    );

    expect(items).toEqual([
      { node: expect.objectContaining({ parsedTitle: "A" }), progress: 0.5 },
      {
        node: fileNode({ name: "b.mkv", type: "file", relativePath: "Moved/b.mkv" }),
        progress: 0.25,
      },
    ]);
  });

  it("renders applied transitional and terminal TV outcomes instead of calling them pending", () => {
    expect(isPlaybackPending(session(3, "loading", "playing"))).toBe(false);
    expect(isPlaybackPending(session(3, "buffering", "playing"))).toBe(false);
    expect(isPlaybackPending(session(3, "error", "playing"))).toBe(false);
    expect(isPlaybackPending(session(3, "ended", "playing"))).toBe(false);
  });
});
