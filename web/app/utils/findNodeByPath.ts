import type { FileNode } from "~~/generated/rivet/types/models";

export const findNodeByPath = (
  root: FileNode | null,
  relativePath: string,
): FileNode | null => {
  if (!root) return null;
  if (root.relativePath === relativePath) return root;
  if (!root.children) return null;
  for (const child of root.children) {
    const found = findNodeByPath(child, relativePath);
    if (found) return found;
  }
  return null;
};
