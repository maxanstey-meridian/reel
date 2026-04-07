import { ref } from "vue";
import { favouritesAll, favouritesAdd } from "~~/generated/rivet/client/reel";
// eslint-disable-next-line no-restricted-imports
import { rivetFetch } from "~~/generated/rivet/rivet";
import type { Favourite } from "~~/generated/rivet/types/models";

// Rivet's contract API doesn't model query params on DELETE, so the generated
// favouritesRemove() is parameterless. This typed wrapper uses rivetFetch directly
// (same auth/baseUrl infrastructure) to pass the path query param.
const removeFavourite = (path: string): Promise<void> =>
  rivetFetch("DELETE", `/api/favourites`, { query: { path } });

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

    // Optimistic update
    if (wasFavourited) {
      favourites.value = favourites.value.filter((f) => f.relativePath !== relativePath);
    } else {
      favourites.value = [{ relativePath, addedAt: new Date().toISOString() }, ...favourites.value];
    }

    try {
      if (wasFavourited) {
        await removeFavourite(relativePath);
      } else {
        await favouritesAdd({ relativePath });
      }
    } catch (e) {
      // Revert optimistic update
      if (wasFavourited) {
        favourites.value = [
          { relativePath, addedAt: new Date().toISOString() },
          ...favourites.value,
        ];
      } else {
        favourites.value = favourites.value.filter((f) => f.relativePath !== relativePath);
      }
      error.value = e instanceof Error ? e.message : "Failed to toggle favourite";
    }
  };

  return { favourites, favouritePaths, loading, error, isFavourite, toggle, fetch };
};
