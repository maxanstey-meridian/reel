import { client, unwrap, type FileNode } from "~~/app/lib/api";

export const useLibrary = () => {
  const {
    data: tree,
    status,
    error,
    refresh,
  } = useBrowseData<FileNode | null>(
    "library",
    async (signal) => unwrap(await client.GET("/api/library", { signal })),
    () => null,
  );
  const loading = computed(() => status.value === "pending" && !tree.value);
  // A rescan supersedes any tree still loading from before it.
  const { rescan, rescanning } = useRescan(() => refresh({ dedupe: "cancel" }));
  return { tree, loading, error, refresh, rescan, rescanning };
};
