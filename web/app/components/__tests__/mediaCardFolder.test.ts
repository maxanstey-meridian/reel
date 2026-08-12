import { describe, expect, it } from "vitest";
import { countDirectFiles } from "~~/app/utils/directoryHelpers";
import type { FileNode } from "~~/app/lib/api";

const file = (name: string): FileNode => ({
  name,
  type: "file",
  relativePath: name,
});

describe("countDirectFiles", () => {
  it("counts only direct file children", () => {
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

    expect(countDirectFiles(node)).toBe(2);
  });

  it("returns zero without children", () => {
    expect(countDirectFiles({ name: "Empty", type: "directory", relativePath: "Empty" })).toBe(0);
  });
});
