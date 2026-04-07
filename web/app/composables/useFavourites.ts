import { ref } from "vue";
import { useConnection } from "./useConnection";

export type Favourite = {
  relativePath: string;
  addedAt: string;
};

export const useFavourites = () => {
  const favourites = useState<Favourite[]>("reel:favourites", () => []);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const { serverUrl, secretKey } = useConnection();

  const favouritePaths = computed(
    () => new Set(favourites.value.map((f) => f.relativePath)),
  );

  const isFavourite = (relativePath: string) =>
    favouritePaths.value.has(relativePath);

  const authFetch = (path: string, options: RequestInit = {}) =>
    globalThis.fetch(`${serverUrl.value}${path}`, {
      ...options,
      headers: {
        "X-Secret-Key": secretKey.value,
        ...options.headers,
      },
    });

  const fetch = async () => {
    loading.value = true;
    error.value = null;

    try {
      const response = await authFetch("/api/favourites");

      if (!response.ok) {
        throw new Error(`Failed to fetch favourites: ${response.status}`);
      }

      favourites.value = await response.json();
    } catch (e) {
      error.value =
        e instanceof Error ? e.message : "Failed to fetch favourites";
    } finally {
      loading.value = false;
    }
  };

  const toggle = async (relativePath: string) => {
    const wasFavourited = isFavourite(relativePath);

    // Optimistic update
    if (wasFavourited) {
      favourites.value = favourites.value.filter(
        (f) => f.relativePath !== relativePath,
      );
    } else {
      favourites.value = [
        { relativePath, addedAt: new Date().toISOString() },
        ...favourites.value,
      ];
    }

    try {
      if (wasFavourited) {
        const response = await authFetch(
          `/api/favourites?path=${encodeURIComponent(relativePath)}`,
          { method: "DELETE" },
        );
        if (!response.ok) throw new Error(`DELETE failed: ${response.status}`);
      } else {
        const response = await authFetch("/api/favourites", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ relativePath }),
        });
        if (!response.ok) throw new Error(`POST failed: ${response.status}`);
      }
    } catch (e) {
      // Revert optimistic update
      if (wasFavourited) {
        favourites.value = [
          { relativePath, addedAt: new Date().toISOString() },
          ...favourites.value,
        ];
      } else {
        favourites.value = favourites.value.filter(
          (f) => f.relativePath !== relativePath,
        );
      }
      error.value =
        e instanceof Error ? e.message : "Failed to toggle favourite";
    }
  };

  return { favourites, favouritePaths, loading, error, isFavourite, toggle, fetch };
};
