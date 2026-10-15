import type { FileNode } from "~~/app/lib/api";

export const countFilesRecursive = (node: FileNode): number =>
  (node.children ?? []).reduce(
    (total, child) => total + (child.type === "file" ? 1 : countFilesRecursive(child)),
    0,
  );

export const findNodeByPath = (root: FileNode | null, relativePath: string): FileNode | null => {
  if (!root || root.relativePath === relativePath) {
    return root;
  }
  for (const child of root.children ?? []) {
    const found = findNodeByPath(child, relativePath);
    if (found) {
      return found;
    }
  }
  return null;
};

// A file missing from the tree (library unavailable, or the file moved) still shows by path.
export const fileNodeAt = (root: FileNode | null, relativePath: string): FileNode =>
  findNodeByPath(root, relativePath) ?? {
    name: relativePath.split("/").pop() ?? relativePath,
    type: "file",
    relativePath,
  };

export const formatFileSize = (bytes: number): string => {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(0)} KB`;
  }
  if (bytes < 1024 * 1024 * 1024) {
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
};

export const libraryLink = (relativePath: string): string =>
  ["/library", ...relativePath.split("/").filter(Boolean).map(encodeURIComponent)].join("/");

// A folder holding just one file, the usual "Movie (Year)/Movie.mkv" shape, opens that file directly.
export const mediaLink = (node: FileNode): string => {
  const [only, ...others] = node.children ?? [];
  const target = only?.type === "file" && others.length === 0 ? only : node;
  return target.type === "file"
    ? `/detail/${encodeURIComponent(target.relativePath)}`
    : libraryLink(target.relativePath);
};
