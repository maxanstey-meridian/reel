import { ref } from "vue";
import { useConnection } from "./useConnection";

export type WatchProgress = {
  relativePath: string;
  positionSeconds: number;
  durationSeconds: number;
  lastWatched: string;
};

export const useProgress = () => {
  const entries = useState<WatchProgress[]>("reel:progress", () => []);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const { serverUrl, secretKey } = useConnection();

  const fetch = async () => {
    loading.value = true;
    error.value = null;

    try {
      const response = await globalThis.fetch(
        `${serverUrl.value}/api/progress`,
        { headers: { "X-Secret-Key": secretKey.value } },
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch progress: ${response.status}`);
      }

      entries.value = await response.json();
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Failed to fetch progress";
    } finally {
      loading.value = false;
    }
  };

  return { entries, loading, error, fetch };
};
