import type { PlaybackSession } from "~~/app/lib/api";

export const clampPosition = (position: number, duration: number): number => {
  if (!Number.isFinite(position) || position < 0) {
    return 0;
  }
  if (!Number.isFinite(duration) || duration <= 0) {
    return position;
  }
  return Math.min(position, duration);
};

export const formatPlaybackTime = (seconds: number): string => {
  const safe = Math.floor(clampPosition(seconds, Number.POSITIVE_INFINITY));
  const hours = Math.floor(safe / 3600);
  const minutes = Math.floor((safe % 3600) / 60);
  const remainder = safe % 60;
  return hours > 0
    ? `${hours}:${minutes.toString().padStart(2, "0")}:${remainder.toString().padStart(2, "0")}`
    : `${minutes}:${remainder.toString().padStart(2, "0")}`;
};

export const isPlaybackPending = (session: PlaybackSession | null): boolean => {
  if (!session) {
    return false;
  }
  const observation = session.observation;
  if (!observation || observation.appliedRevision < session.revision) {
    return true;
  }
  const pendingSeek = session.intent.pendingSeek;
  if (pendingSeek && (observation.appliedSeekId ?? -1) < pendingSeek.seekId) {
    return true;
  }
  if (["loading", "buffering", "ended", "error"].includes(observation.state)) {
    return false;
  }
  const confirmedState = {
    playing: "playing",
    paused: "paused",
    stopped: "idle",
  } as const;
  return observation.state !== confirmedState[session.intent.desiredState];
};

type Progress = { positionSeconds: number; durationSeconds: number };

export const progressFraction = (progress: Progress | undefined): number =>
  progress && progress.durationSeconds > 0
    ? clampPosition(progress.positionSeconds, progress.durationSeconds) / progress.durationSeconds
    : 0;

// Progress past 95% counts as watched, so it neither resumes nor appears in Continue Watching.
export const hasIncompleteProgress = (progress: Progress | undefined): boolean => {
  const fraction = progressFraction(progress);
  return fraction > 0 && fraction < 0.95;
};
