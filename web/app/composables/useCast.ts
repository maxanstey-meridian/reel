import { ref } from "vue";
import { cast as rivetCast, castStop, castQueuePlay, castQueueGet } from "~~/generated/rivet/client/reel";

export const useCast = () => {
  const casting = ref(false);
  const currentFile = useState<string | null>("reel:currentFile", () => null);
  const queue = useState<string[]>("reel:castQueue", () => []);
  const error = ref<string | null>(null);

  const cast = async (relativePath: string, opts?: { subtitleIndex?: number }) => {
    casting.value = true;
    error.value = null;

    try {
      await rivetCast({ relativePath, subtitleIndex: opts?.subtitleIndex ?? null });
      currentFile.value = relativePath;

      const toast = useToast();
      toast.add({
        title: "Casting to TV",
        description: relativePath.split("/").pop() ?? relativePath,
        color: "success",
        icon: "i-lucide-cast",
      });
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Cast failed";
      currentFile.value = null;
    } finally {
      casting.value = false;
    }
  };

  const castQueue = async (paths: string[]) => {
    if (paths.length === 0) return;
    casting.value = true;
    error.value = null;

    try {
      await castQueuePlay({ paths });
      currentFile.value = paths[0]!;
      queue.value = paths.slice(1);

      const toast = useToast();
      toast.add({
        title: "Casting queue to TV",
        description: `${paths.length} files queued`,
        color: "success",
        icon: "i-lucide-cast",
      });
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Cast queue failed";
      currentFile.value = null;
      queue.value = [];
    } finally {
      casting.value = false;
    }
  };

  const fetchQueue = async () => {
    try {
      queue.value = await castQueueGet();
    } catch {
      queue.value = [];
    }
  };

  const stop = async () => {
    error.value = null;

    try {
      await castStop();
      currentFile.value = null;
      queue.value = [];
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Stop failed";
    }
  };

  return { casting, currentFile, queue, error, cast, castQueue, fetchQueue, stop };
};
