import { client, unwrap } from "~~/app/lib/api";
import type { Tags } from "~~/app/ports/tags";

export const useTags = () => {
  const { data: items, status } = useBrowseData<Record<string, string[]>>(
    "tags",
    async (signal) => unwrap(await client.GET("/api/tags", { signal })),
    () => ({}),
  );
  // Untagged items count as #media, matching server-side tag search.
  const forPath = (path: string) => items.value[path] ?? ["media"];
  const loaded = computed(() => status.value === "success");
  const save = async (path: string, tags: string[]) => {
    const saved = unwrap(await client.PUT("/api/tags", { body: { path, tags } }));
    // Merge into the latest tags: a load or another save may have landed while this one was in flight.
    items.value = { ...items.value, [path]: saved };
  };
  const implementation = { forPath, loaded, save };
  implementation satisfies Tags;
  return implementation;
};
