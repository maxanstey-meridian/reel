import { describe, it, expect } from "vitest";
import { findFirstFile, countDirectFiles } from "~~/app/utils/directoryHelpers";
import type { FileNode } from "~~/generated/rivet/types/domain";

const makeNode = (overrides: Partial<FileNode> = {}): FileNode => ({
  name: "Some.File.mp4",
  type: "file",
  relativePath: "Films/Some.File.mp4",
  extension: ".mp4",
  sizeBytes: 1_000_000,
  children: null,
  parsedTitle: null,
  year: null,
  thumbnailUrl: null,
  watched: null,
  displayTitle: null,
  ...overrides,
});

const makeDir = (name: string, children: FileNode[]): FileNode =>
  makeNode({
    name,
    type: "directory",
    relativePath: name,
    extension: null,
    sizeBytes: null,
    children,
    displayTitle: null,
  });

describe("findFirstFile", () => {
  it("returns the first file from flat children", () => {
    const fileA = makeNode({ name: "A.mp4", relativePath: "Dir/A.mp4" });
    const fileB = makeNode({ name: "B.mp4", relativePath: "Dir/B.mp4" });
    const dir = makeDir("Dir", [fileA, fileB]);

    expect(findFirstFile(dir)).toBe(fileA);
  });

  it("finds a file nested inside subdirectories", () => {
    const deepFile = makeNode({ name: "Deep.mp4", relativePath: "A/B/Deep.mp4" });
    const innerDir = makeDir("B", [deepFile]);
    const outerDir = makeDir("A", [innerDir]);

    expect(findFirstFile(outerDir)).toBe(deepFile);
  });

  it("returns null for an empty directory", () => {
    const dir = makeDir("Empty", []);
    expect(findFirstFile(dir)).toBeNull();
  });

  it("returns null when children is null", () => {
    const dir = makeNode({ type: "directory", children: null });
    expect(findFirstFile(dir)).toBeNull();
  });

  it("returns null when all descendants are empty directories", () => {
    const emptyA = makeDir("A", []);
    const emptyB = makeDir("B", []);
    const dir = makeDir("Root", [emptyA, emptyB]);

    expect(findFirstFile(dir)).toBeNull();
  });

  it("prefers a direct file child over a nested file", () => {
    const directFile = makeNode({ name: "Direct.mp4", relativePath: "Dir/Direct.mp4" });
    const nestedFile = makeNode({ name: "Nested.mp4", relativePath: "Dir/Sub/Nested.mp4" });
    const subDir = makeDir("Sub", [nestedFile]);
    const dir = makeDir("Dir", [subDir, directFile]);

    // Direct files are checked first across all children, then subdirs are recursed
    expect(findFirstFile(dir)).toBe(directFile);
  });
});

describe("countDirectFiles", () => {
  it("counts only direct file children", () => {
    const files = [
      makeNode({ name: "A.mp4" }),
      makeNode({ name: "B.mp4" }),
      makeNode({ name: "C.mp4" }),
    ];
    const subDir = makeDir("Sub", [makeNode({ name: "D.mp4" })]);
    const dir = makeDir("Dir", [...files, subDir]);

    expect(countDirectFiles(dir)).toBe(3);
  });

  it("returns 0 when directory has only subdirectory children", () => {
    const subA = makeDir("A", [makeNode()]);
    const subB = makeDir("B", [makeNode()]);
    const dir = makeDir("Dir", [subA, subB]);

    expect(countDirectFiles(dir)).toBe(0);
  });

  it("returns 0 for empty directory", () => {
    const dir = makeDir("Empty", []);
    expect(countDirectFiles(dir)).toBe(0);
  });

  it("returns 0 when children is null", () => {
    const dir = makeNode({ type: "directory", children: null });
    expect(countDirectFiles(dir)).toBe(0);
  });

  it("returns 1 for a single file child", () => {
    const dir = makeDir("Dir", [makeNode({ name: "Only.mp4" })]);
    expect(countDirectFiles(dir)).toBe(1);
  });
});
