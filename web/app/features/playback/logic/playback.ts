import { fileNodeAt } from "~~/app/features/library/logic/library";
import type { FileNode, PlaybackSession } from "~~/app/lib/api";

export type TvState = NonNullable<PlaybackSession["observation"]>["state"];

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
  // The server clears a seek once the TV reports applying it.
  if (session.intent.pendingSeek) {
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

const tvStateLabels: Record<TvState, string> = {
  playing: "Playing on the TV",
  paused: "Paused",
  idle: "Stopped",
  loading: "Loading on the TV",
  buffering: "Buffering on the TV",
  ended: "Finished",
  error: "Couldn't play on the TV",
};

/** What the TV last reported, once it has caught up with the latest control. */
export const describeTvState = (
  session: Pick<PlaybackSession, "tvConnected" | "observation">,
  pending: boolean,
): string => {
  if (!session.tvConnected) {
    return "TV disconnected";
  }
  const state = session.observation?.state;
  return pending || !state ? "Waiting for the TV…" : tvStateLabels[state];
};

export const formatDuration = (seconds: number): string => {
  const safe = clampPosition(seconds, Number.POSITIVE_INFINITY);
  const hours = Math.floor(safe / 3600);
  const minutes = Math.floor((safe % 3600) / 60);
  return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
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

type ProgressEntry = Progress & { relativePath: string };

/** Unfinished files, in the server's order, including ones missing from the library tree. */
export const continueWatching = (
  entries: readonly ProgressEntry[],
  tree: FileNode | null,
): { node: FileNode; progress: number }[] =>
  entries.filter(hasIncompleteProgress).map((entry) => ({
    node: fileNodeAt(tree, entry.relativePath),
    progress: progressFraction(entry),
  }));
