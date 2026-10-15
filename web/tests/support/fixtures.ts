import type { FileNode, PlaybackSession } from "~~/app/lib/api";

type Intent = PlaybackSession["intent"];
type Observation = NonNullable<PlaybackSession["observation"]>;

// The server writes every nullable field, so fixtures default them to null and name only what a test needs.
export const fileNode = (
  fields: Pick<FileNode, "name" | "type" | "relativePath"> & Partial<FileNode>,
): FileNode => ({
  kind: null,
  extension: null,
  sizeBytes: null,
  children: null,
  parsedTitle: null,
  year: null,
  ...fields,
});

export const intent = (
  fields: Omit<Intent, "subtitleIndex" | "pendingSeek"> & Partial<Intent>,
): Intent => ({ subtitleIndex: null, pendingSeek: null, ...fields });

export const observation = (
  fields: Omit<Observation, "appliedSeekId" | "subtitleIndex" | "errorCode" | "errorMessage"> &
    Partial<Observation>,
): Observation => ({
  appliedSeekId: null,
  subtitleIndex: null,
  errorCode: null,
  errorMessage: null,
  ...fields,
});
