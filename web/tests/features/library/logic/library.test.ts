import { describe, expect, it } from "vitest";
import {
  countFilesRecursive,
  formatFileSize,
  mediaLink,
} from "~~/app/features/library/logic/library";
import type { FileNode } from "~~/app/lib/api";
import { fileNode } from "../../../support/fixtures";

const file = (name: string): FileNode => fileNode({ name, type: "file", relativePath: name });

describe("countFilesRecursive", () => {
  it("counts files in nested folders", () => {
    const node = fileNode({
      name: "Dir",
      type: "directory",
      relativePath: "Dir",
      children: [
        file("A.mp4"),
        file("B.mp4"),
        fileNode({
          name: "Sub",
          type: "directory",
          relativePath: "Dir/Sub",
          children: [file("C.mp4")],
        }),
      ],
    });
    expect(countFilesRecursive(node)).toBe(3);
  });

  it("returns zero without children", () => {
    expect(
      countFilesRecursive(fileNode({ name: "Empty", type: "directory", relativePath: "Empty" })),
    ).toBe(0);
  });
});

describe("formatFileSize", () => {
  it("uses the largest unit that keeps the number readable", () => {
    expect(formatFileSize(512)).toBe("512 B");
    expect(formatFileSize(2048)).toBe("2 KB");
    expect(formatFileSize(1.5 * 1024 * 1024)).toBe("1.5 MB");
    expect(formatFileSize(4.25 * 1024 * 1024 * 1024)).toBe("4.25 GB");
  });
});

describe("mediaLink", () => {
  it("opens a folder holding one file at that file, and other folders as folders", () => {
    const single = fileNode({
      name: "Film",
      type: "directory",
      relativePath: "Film",
      children: [file("Film/Film.mkv")],
    });
    const pair: FileNode = { ...single, children: [file("Film/a.mkv"), file("Film/b.mkv")] };

    expect(mediaLink(single)).toBe(`/detail/${encodeURIComponent("Film/Film.mkv")}`);
    expect(mediaLink(pair)).toBe("/library/Film");
  });
});
