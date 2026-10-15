import { client, unwrap, type FileNode } from "~~/app/lib/api";
import type { Tags } from "~~/app/ports/tags";

export const useTags = () => {
  const {
    data: items,
    status,
    error,
    refresh,
  } = useBrowseData<Record<string, string[]>>(
    "tags",
    async (signal) => unwrap(await client.GET("/api/tags", { signal })),
    () => ({}),
  );
  // Untagged files count as #media, matching server-side tag search. A folder's tags are
  // inherited by every file in it, so an untagged folder has none.
  const forPath = (path: string, type: FileNode["type"]) =>
    items.value[path] ?? (type === "file" ? ["media"] : []);
  const loaded = computed(() => status.value === "success");
  const save = async (path: string, tags: string[]) => {
    const saved = unwrap(await client.PUT("/api/tags", { body: { path, tags } }));
    // Merge into the latest tags: a load or another save may have landed while this one was in flight.
    items.value = { ...items.value, [path]: saved };
  };
  const implementation = { forPath, loaded, error, refresh, save };
  implementation satisfies Tags;
  return implementation;
};
