import { ref } from "vue";
import { mediaSetWatched, mediaSetUnwatched, mediaClearWatched } from "~~/generated/rivet/client/reel";

export const useWatchState = () => {
  const loading = ref(false);

  const setWatched = async (path: string) => {
    loading.value = true;
    try {
      await mediaSetWatched({ path });
      await useLibrary().refresh();
    } finally {
      loading.value = false;
    }
  };

  const setUnwatched = async (path: string) => {
    loading.value = true;
    try {
      await mediaSetUnwatched({ path });
      await useLibrary().refresh();
    } finally {
      loading.value = false;
    }
  };

  const clearWatchState = async (path: string) => {
    loading.value = true;
    try {
      await mediaClearWatched(path);
      await useLibrary().refresh();
    } finally {
      loading.value = false;
    }
  };

  return { loading, setWatched, setUnwatched, clearWatchState };
};
