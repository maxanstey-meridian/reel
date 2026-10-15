import { describe, expect, it } from "vitest";
import { countFilesRecursive } from "~~/app/features/library/logic/library";
import type { FileNode } from "~~/app/lib/api";

const file = (name: string): FileNode => ({ name, type: "file", relativePath: name });

describe("countFilesRecursive", () => {
  it("counts files in nested folders", () => {
    const node: FileNode = {
      name: "Dir",
      type: "directory",
      relativePath: "Dir",
      children: [
        file("A.mp4"),
        file("B.mp4"),
        { name: "Sub", type: "directory", relativePath: "Dir/Sub", children: [file("C.mp4")] },
      ],
    };
    expect(countFilesRecursive(node)).toBe(3);
  });

  it("returns zero without children", () => {
    expect(countFilesRecursive({ name: "Empty", type: "directory", relativePath: "Empty" })).toBe(
      0,
    );
  });
});
