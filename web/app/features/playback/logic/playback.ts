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

/** The TV's report once it has applied the latest request, else null. */
const caughtUpObservation = (session: PlaybackSession) =>
  session.observation && session.observation.appliedRevision >= session.revision
    ? session.observation
    : null;

export const isPlaybackPending = (session: PlaybackSession | null): boolean => {
  if (!session) {
    return false;
  }
  const observation = caughtUpObservation(session);
  if (!observation) {
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

/**
 * The subtitle to show as chosen: the requested one until the TV has applied that request, then the one the TV
 * reports (null when it couldn't show the request), so a pick never appears to snap back while it travels.
 */
export const shownSubtitleIndex = (session: PlaybackSession): number | null => {
  const observation = caughtUpObservation(session);
  return observation ? observation.subtitleIndex : session.intent.subtitleIndex;
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

/** A TV that reported and then went quiet, as opposed to one that hasn't picked the session up yet. */
export const isTvLost = (session: Pick<PlaybackSession, "tvConnected" | "observation">): boolean =>
  !session.tvConnected && session.observation != null;

/** What the TV last reported, once it has caught up with the latest control. */
export const describeTvState = (
  session: Pick<PlaybackSession, "tvConnected" | "observation">,
  pending: boolean,
): string => {
  if (!session.observation) {
    return "Waiting for the TV…";
  }
  if (isTvLost(session)) {
    return "TV disconnected";
  }
  return pending ? "Waiting for the TV…" : tvStateLabels[session.observation.state];
};

export const formatDuration = (seconds: number): string => {
  const safe = clampPosition(seconds, Number.POSITIVE_INFINITY);
  const hours = Math.floor(safe / 3600);
  const minutes = Math.floor((safe % 3600) / 60);
  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }
  return minutes > 0 ? `${minutes}m` : "<1m";
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
