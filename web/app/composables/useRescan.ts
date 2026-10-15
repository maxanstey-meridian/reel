import { client, unwrap } from "~~/app/lib/api";

/** Re-indexes the media folder so new files become searchable, then reloads what is on screen. */
export const useRescan = (reload: () => Promise<void>) => {
  const toast = useToast();
  const rescanning = ref(false);
  const rescan = async (): Promise<void> => {
    rescanning.value = true;
    try {
      unwrap(await client.POST("/api/library/rescan"));
      await reload();
    } catch (caught) {
      toast.add({
        title: "Couldn't look for new files",
        description: (caught as Error).message,
        color: "error",
      });
    } finally {
      rescanning.value = false;
    }
  };
  return { rescan, rescanning };
};
