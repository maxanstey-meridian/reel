import { describe, it, expect } from "vitest";
import { parseFilename } from "~~/app/utils/parseFilename";
import type { FileNode } from "~~/generated/rivet/types/domain";

/**
 * These tests verify the display title resolution logic used by
 * MediaCard.vue and FileDetail.vue:
 *   title = node.displayTitle ?? node.name
 *   year  = parseFilename(node.name).year  (still from filename)
 */

const makeNode = (overrides: Partial<FileNode> = {}): FileNode => ({
  name: "The.Matrix.1999.1080p.BluRay.x264.mp4",
  type: "file",
  relativePath: "Films/The.Matrix.1999.1080p.BluRay.x264.mp4",
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

const resolveDisplayName = (node: FileNode): string =>
  node.displayTitle ?? node.name;

describe("displayTitle resolution", () => {
  it("uses displayTitle when present", () => {
    const node = makeNode({ displayTitle: "The Matrix" });
    expect(resolveDisplayName(node)).toBe("The Matrix");
  });

  it("falls back to node.name when displayTitle is null", () => {
    const node = makeNode({ displayTitle: null });
    expect(resolveDisplayName(node)).toBe("The.Matrix.1999.1080p.BluRay.x264.mp4");
  });

  it("falls back to node.name for directories (no displayTitle)", () => {
    const node = makeNode({
      type: "directory",
      name: "Films",
      displayTitle: null,
      extension: null,
      sizeBytes: null,
    });
    expect(resolveDisplayName(node)).toBe("Films");
  });

  it("year badge still derives from parseFilename regardless of displayTitle", () => {
    const node = makeNode({ displayTitle: "The Matrix" });
    const parsed = parseFilename(node.name);
    expect(parsed.year).toBe(1999);
  });

  it("year badge works even when displayTitle is null", () => {
    const node = makeNode({ displayTitle: null });
    const parsed = parseFilename(node.name);
    expect(parsed.year).toBe(1999);
  });

  it("prefers server displayTitle over client-parsed title", () => {
    const node = makeNode({
      name: "badly.named.file.mp4",
      displayTitle: "Actually A Good Title",
    });
    expect(resolveDisplayName(node)).toBe("Actually A Good Title");
    // parseFilename would produce something different
    expect(parseFilename(node.name).title).not.toBe("Actually A Good Title");
  });
});
