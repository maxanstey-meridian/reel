import { ref } from "vue";
import { api, type WatchProgress } from "~~/app/lib/api";

export type { WatchProgress };

export const useProgress = () => {
  const entries = useState<WatchProgress[]>("reel:progress", () => []);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const fetch = async () => {
    loading.value = true;
    error.value = null;

    try {
      entries.value = await api.progressAll();
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Failed to fetch progress";
    } finally {
      loading.value = false;
    }
  };

  return { entries, loading, error, fetch };
};
