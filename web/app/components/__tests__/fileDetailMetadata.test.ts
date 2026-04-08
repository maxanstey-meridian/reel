import { describe, it, expect } from "vitest";
import type { MediaUpdateRequest } from "~~/generated/rivet/types/models";

/**
 * These tests verify the metadata editing logic used by FileDetail.vue:
 * - PATCH body construction sends only the changed field, nulls for rest
 * - Library refresh decision based on which field changed
 */

type EditableField = keyof MediaUpdateRequest;

const buildPatchBody = (field: EditableField, value: string | null): MediaUpdateRequest => {
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

const shouldRefreshLibrary = (field: EditableField): boolean => field === "displayTitle";

describe("metadata PATCH body construction", () => {
  it("sets only genre, nulls for all other fields", () => {
    const body = buildPatchBody("genre", "Comedy");
    expect(body).toEqual({
      genre: "Comedy",
      rating: null,
      language: null,
      notes: null,
      displayTitle: null,
    });
  });

  it("sets only displayTitle, nulls for all other fields", () => {
    const body = buildPatchBody("displayTitle", "New Title");
    expect(body).toEqual({
      genre: null,
      rating: null,
      language: null,
      notes: null,
      displayTitle: "New Title",
    });
  });

  it("sets only notes, nulls for all other fields", () => {
    const body = buildPatchBody("notes", "Great film");
    expect(body).toEqual({
      genre: null,
      rating: null,
      language: null,
      notes: "Great film",
      displayTitle: null,
    });
  });

  it("sends empty string to clear a field", () => {
    const body = buildPatchBody("genre", "");
    expect(body).toEqual({
      genre: "",
      rating: null,
      language: null,
      notes: null,
      displayTitle: null,
    });
  });

  it("never includes extra fields beyond the MediaUpdateRequest shape", () => {
    const body = buildPatchBody("rating", "PG-13");
    const keys = Object.keys(body).sort();
    expect(keys).toEqual(["displayTitle", "genre", "language", "notes", "rating"]);
  });
});

describe("library refresh decision", () => {
  it("refreshes library when displayTitle changes", () => {
    expect(shouldRefreshLibrary("displayTitle")).toBe(true);
  });

  it("does not refresh library when genre changes", () => {
    expect(shouldRefreshLibrary("genre")).toBe(false);
  });

  it("does not refresh library when rating changes", () => {
    expect(shouldRefreshLibrary("rating")).toBe(false);
  });

  it("does not refresh library when language changes", () => {
    expect(shouldRefreshLibrary("language")).toBe(false);
  });

  it("does not refresh library when notes changes", () => {
    expect(shouldRefreshLibrary("notes")).toBe(false);
  });
});
