import type { FileNode } from "~~/generated/rivet/types/domain";

/**
 * Recursively walks a directory node depth-first and returns the first
 * descendant with type === 'file', or null if none exist.
 */
export const findFirstFile = (node: FileNode): FileNode | null => {
  if (!node.children) {
    return null;
  }

  for (const child of node.children) {
    if (child.type === "file") {
      return child;
    }
  }

  for (const child of node.children) {
    if (child.type === "directory") {
      const found = findFirstFile(child);
      if (found) {
        return found;
      }
    }
  }

  return null;
};

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
