import { ref } from "vue";
import { mediaClearWatched } from "~~/generated/rivet/client/reel";
import { rivetFetch } from "~~/generated/rivet/rivet";

export const useWatchState = () => {
  const loading = ref(false);

  // rivetFetch used directly because generated mediaSetWatched() sends path as
  // JSON body, but server reads path from [FromQuery] — codegen doesn't support
  // mixed body+query for PUT routes
  const setWatched = async (path: string) => {
    loading.value = true;
    try {
      await rivetFetch("PUT", "/api/media/watched", { body: { path }, query: { path } });
      await useLibrary().refresh();
    } finally {
      loading.value = false;
    }
  };

  const setUnwatched = async (path: string) => {
    loading.value = true;
    try {
      await rivetFetch("PUT", "/api/media/unwatched", { body: { path }, query: { path } });
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
