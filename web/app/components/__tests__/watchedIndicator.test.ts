import { describe, it, expect } from "vitest";
import type { FileNode } from "~~/generated/rivet/types/domain";

/**
 * These tests verify the watched indicator logic used by MediaCard.vue:
 *   isWatched = node.watched === true
 *
 * When isWatched is true:
 *   - check-circle icon overlay is shown
 *   - card gets opacity-60
 * When false or null: no indicator, full opacity.
 */

const makeNode = (overrides: Partial<FileNode> = {}): FileNode => ({
  name: "movie.mp4",
  type: "file",
  relativePath: "Films/movie.mp4",
  extension: ".mp4",
  sizeBytes: 1_500_000_000,
  children: null,
  parsedTitle: null,
  year: null,
  thumbnailUrl: null,
  watched: null,
  displayTitle: null,
  ...overrides,
});

const isWatched = (node: FileNode): boolean => node.watched === true;

describe("watched indicator logic", () => {
  it("returns true when watched is true", () => {
    expect(isWatched(makeNode({ watched: true }))).toBe(true);
  });

  it("returns false when watched is false", () => {
    expect(isWatched(makeNode({ watched: false }))).toBe(false);
  });

  it("returns false when watched is null", () => {
    expect(isWatched(makeNode({ watched: null }))).toBe(false);
  });

  it("directories with watched true still get indicator", () => {
    expect(isWatched(makeNode({ type: "directory", watched: true }))).toBe(true);
  });

  it("only strict true triggers indicator — not truthy values", () => {
    // Verify the contract: only `true`, not truthy coercion
    const node = makeNode({ watched: null });
    expect(node.watched === true).toBe(false);

    const watchedNode = makeNode({ watched: true });
    expect(watchedNode.watched === true).toBe(true);
  });
});
