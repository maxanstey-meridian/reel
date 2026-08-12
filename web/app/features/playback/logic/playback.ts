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

export const playbackTitle = (relativePath: string): string => {
  const fileName = relativePath.split("/").pop() ?? relativePath;
  return fileName.replace(/\.[^.]+$/, "").replace(/[._]+/g, " ");
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

export const hasIncompleteProgress = (
  progress: { positionSeconds: number; durationSeconds: number } | undefined,
): boolean =>
  !!progress &&
  progress.positionSeconds > 0 &&
  progress.durationSeconds > 0 &&
  progress.positionSeconds / progress.durationSeconds < 0.9;
