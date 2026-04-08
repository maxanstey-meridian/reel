import { ref } from "vue";
import { favouritesAll, favouritesAdd, favouritesRemove } from "~~/generated/rivet/client/reel";
import type { Favourite } from "~~/generated/rivet/types/domain";

export type { Favourite };

export const useFavourites = () => {
  const favourites = useState<Favourite[]>("reel:favourites", () => []);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const favouritePaths = computed(() => new Set(favourites.value.map((f) => f.relativePath)));

  const isFavourite = (relativePath: string) => favouritePaths.value.has(relativePath);

  const fetch = async () => {
    loading.value = true;
    error.value = null;

    try {
      favourites.value = await favouritesAll();
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Failed to fetch favourites";
    } finally {
      loading.value = false;
    }
  };

  const toggle = async (relativePath: string) => {
    const wasFavourited = isFavourite(relativePath);
    const original = favourites.value.find((f) => f.relativePath === relativePath);

    // Optimistic update
    if (wasFavourited) {
      favourites.value = favourites.value.filter((f) => f.relativePath !== relativePath);
    } else {
      favourites.value = [{ relativePath, addedAt: new Date().toISOString() }, ...favourites.value];
    }

    try {
      if (wasFavourited) {
        await favouritesRemove(relativePath);
        const toast = useToast();
        toast.add({ title: "Removed from favourites", icon: "i-lucide-heart-off", color: "neutral" });
      } else {
        await favouritesAdd({ relativePath });
        const toast = useToast();
        toast.add({ title: "Added to favourites", icon: "i-lucide-heart", color: "success" });
      }
    } catch (e) {
      // Revert optimistic update
      if (wasFavourited && original) {
        favourites.value = [original, ...favourites.value];
      } else {
        favourites.value = favourites.value.filter((f) => f.relativePath !== relativePath);
      }
      error.value = e instanceof Error ? e.message : "Failed to toggle favourite";
    }
  };

  return { favourites, favouritePaths, loading, error, isFavourite, toggle, fetch };
};
