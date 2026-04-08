import type { MediaDetailResponse, MediaUpdateRequest } from "~~/generated/rivet/types/models";

export type EditableField = keyof MediaUpdateRequest & keyof MediaDetailResponse;

export const buildPatchBody = (field: EditableField, value: string): MediaUpdateRequest => {
  const body: MediaUpdateRequest = {
    genre: null,
    rating: null,
    language: null,
    notes: null,
    displayTitle: null,
  };
  body[field] = value;
  return body;
};

export const shouldRefreshLibrary = (field: EditableField): boolean => field === "displayTitle";
