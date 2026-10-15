import { client, unwrap } from "~~/app/lib/api";

export const useProgress = () =>
  useBrowseData(
    "progress",
    async (signal) => unwrap(await client.GET("/api/progress", { signal })),
    () => [],
  );
