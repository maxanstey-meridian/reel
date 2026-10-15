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

export const libraryLink = (relativePath: string): string =>
  ["/library", ...relativePath.split("/").filter(Boolean).map(encodeURIComponent)].join("/");

// A folder holding just one file, the usual "Movie (Year)/Movie.mkv" shape, opens that file directly.
export const mediaLink = (node: FileNode): string => {
  const target =
    node.children?.length === 1 && node.children[0]!.type === "file" ? node.children[0]! : node;
  return target.type === "file"
    ? `/detail/${encodeURIComponent(target.relativePath)}`
    : libraryLink(target.relativePath);
};
