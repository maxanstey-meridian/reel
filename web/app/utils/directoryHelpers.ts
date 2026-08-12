import type { FileNode } from "~~/app/lib/api";

/**
 * Counts direct children of a directory node that are files.
 * Returns 0 if the node has no children.
 */
export const countDirectFiles = (node: FileNode): number => {
  if (!node.children) {
    return 0;
  }
  return node.children.filter((child) => child.type === "file").length;
};
