import { client, unwrap } from "~~/app/lib/api";
import type { Favourites } from "~~/app/ports/favourites";

export const useFavourites = () => {
  const toast = useToast();
  const toggling = new Set<string>();
  const {
    data: favourites,
    status,
    error,
    refresh,
  } = useBrowseData(
    "favourites",
    async (signal) => unwrap(await client.GET("/api/favourites", { signal })),
    () => [],
  );
  const loading = computed(() => status.value === "pending" && favourites.value.length === 0);
  const isFavourite = (relativePath: string) =>
    favourites.value.some((f) => f.relativePath === relativePath);
  const without = (relativePath: string) =>
    favourites.value.filter((f) => f.relativePath !== relativePath);

  const toggle = async (relativePath: string) => {
    if (toggling.has(relativePath)) {
      return;
    }
    toggling.add(relativePath);
    const original = favourites.value.find((f) => f.relativePath === relativePath);
    const optimistic = original
      ? without(relativePath)
      : [{ relativePath, addedAt: new Date().toISOString() }, ...favourites.value];
    favourites.value = optimistic;
    try {
      if (original) {
        unwrap(
          await client.DELETE("/api/favourites", { params: { query: { path: relativePath } } }),
        );
        toast.add({
          title: "Removed from favourites",
          icon: "i-lucide-heart-off",
          color: "neutral",
        });
      } else {
        unwrap(await client.POST("/api/favourites", { body: { relativePath } }));
        toast.add({ title: "Added to favourites", icon: "i-lucide-heart", color: "success" });
      }
      // A load that overlapped the change may have answered from before it; ask again now it is saved.
      if (status.value === "pending" || favourites.value !== optimistic) {
        void refresh({ dedupe: "cancel" });
      }
    } catch (caught) {
      favourites.value = original ? [original, ...without(relativePath)] : without(relativePath);
      toast.add({
        title: "Could not update favourite",
        description: (caught as Error).message,
        color: "error",
      });
    } finally {
      toggling.delete(relativePath);
    }
  };

  const implementation = { favourites, loading, error, refresh, isFavourite, toggle };
  implementation satisfies Favourites;
  return implementation;
};
