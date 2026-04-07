import { ref, watch, onUnmounted } from "vue";
import { useConnection } from "./useConnection";

/**
 * Fetches an image from an authenticated endpoint and returns a blob URL.
 * Cleans up the object URL on unmount or when the source changes.
 */
// TODO: Replace with a Rivet call once it supports streaming/downloads.
export const useAuthImage = (urlPath: Ref<string | null>) => {
  const { serverUrl, secretKey } = useConnection();
  const src = ref<string | null>(null);
  const loading = ref(false);
  const error = ref(false);

  let currentObjectUrl: string | null = null;

  const revoke = () => {
    if (currentObjectUrl) {
      URL.revokeObjectURL(currentObjectUrl);
      currentObjectUrl = null;
    }
  };

  const load = async (path: string | null) => {
    revoke();
    src.value = null;
    error.value = false;

    if (!path || !serverUrl.value || !secretKey.value) {
      return;
    }

    loading.value = true;
    try {
      const url = `${serverUrl.value}${path}`;
      const response = await fetch(url, {
        headers: { "X-Secret-Key": secretKey.value },
      });

      if (!response.ok) {
        error.value = true;
        return;
      }

      const blob = await response.blob();
      currentObjectUrl = URL.createObjectURL(blob);
      src.value = currentObjectUrl;
    } catch {
      error.value = true;
    } finally {
      loading.value = false;
    }
  };

  watch(urlPath, (newPath) => load(newPath), { immediate: true });

  onUnmounted(() => revoke());

  return { src, loading, error };
};
