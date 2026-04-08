import { describe, it, expect } from "vitest";
import { buildPatchBody, shouldRefreshLibrary } from "~~/app/utils/metadataEdit";

/**
 * These tests verify the metadata editing logic extracted from FileDetail.vue.
 * FileDetail.vue imports and uses these same functions — if they break here,
 * the component breaks too.
 */

describe("buildPatchBody", () => {
  it("sets only genre, nulls for all other fields", () => {
    expect(buildPatchBody("genre", "Comedy")).toEqual({
      genre: "Comedy",
      rating: null,
      language: null,
      notes: null,
      displayTitle: null,
    });
  });

  it("sets only displayTitle, nulls for all other fields", () => {
    expect(buildPatchBody("displayTitle", "New Title")).toEqual({
      genre: null,
      rating: null,
      language: null,
      notes: null,
      displayTitle: "New Title",
    });
  });

  it("sets only notes, nulls for all other fields", () => {
    expect(buildPatchBody("notes", "Great film")).toEqual({
      genre: null,
      rating: null,
      language: null,
      notes: "Great film",
      displayTitle: null,
    });
  });

  it("sends empty string to clear a field", () => {
    expect(buildPatchBody("genre", "")).toEqual({
      genre: "",
      rating: null,
      language: null,
      notes: null,
      displayTitle: null,
    });
  });

  it("never includes extra fields beyond the MediaUpdateRequest shape", () => {
    const keys = Object.keys(buildPatchBody("rating", "PG-13")).sort();
    expect(keys).toEqual(["displayTitle", "genre", "language", "notes", "rating"]);
  });

  it("saveNotes path: sets only notes field via buildPatchBody", () => {
    // FileDetail.saveNotes calls buildPatchBody("notes", newVal)
    expect(buildPatchBody("notes", "My thoughts")).toEqual({
      genre: null,
      rating: null,
      language: null,
      notes: "My thoughts",
      displayTitle: null,
    });
  });

  it("saveNotes path: clearing notes sends empty string", () => {
    expect(buildPatchBody("notes", "")).toEqual({
      genre: null,
      rating: null,
      language: null,
      notes: "",
      displayTitle: null,
    });
  });
});

describe("shouldRefreshLibrary", () => {
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
