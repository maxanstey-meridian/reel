import { describe, it, expect } from "vitest";
import type { FileNode } from "~~/app/lib/api";

/**
 * These tests verify the display title resolution logic used by
 * MediaCard.vue and the detail page:
 *   title = node.parsedTitle ?? node.name
 *   year  = node.year  (server-parsed from filename)
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
  ...overrides,
});

const resolveDisplayName = (node: FileNode): string => node.parsedTitle ?? node.name;

describe("display title resolution", () => {
  it("uses parsedTitle when present", () => {
    const node = makeNode({ parsedTitle: "The Matrix" });
    expect(resolveDisplayName(node)).toBe("The Matrix");
  });

  it("falls back to node.name when parsedTitle is null", () => {
    const node = makeNode({ parsedTitle: null });
    expect(resolveDisplayName(node)).toBe("The.Matrix.1999.1080p.BluRay.x264.mp4");
  });

  it("falls back to node.name for directories without a parsed title", () => {
    const node = makeNode({
      type: "directory",
      name: "Films",
      parsedTitle: null,
      extension: null,
      sizeBytes: null,
    });
    expect(resolveDisplayName(node)).toBe("Films");
  });

  it("year comes from node.year regardless of parsedTitle", () => {
    const node = makeNode({ parsedTitle: "The Matrix", year: 1999 });
    expect(node.year).toBe(1999);
  });

  it("year is null when server did not parse one", () => {
    const node = makeNode({ parsedTitle: null, year: null });
    expect(node.year).toBeNull();
  });

  it("uses the server-parsed title instead of the filename", () => {
    const node = makeNode({
      name: "badly.named.file.mp4",
      parsedTitle: "The Matrix",
    });
    expect(resolveDisplayName(node)).toBe("The Matrix");
  });
});
