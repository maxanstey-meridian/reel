import { ref } from "vue";
import { cast as rivetCast, castStop } from "~~/generated/rivet/client/reel";

export const useCast = () => {
  const casting = ref(false);
  const currentFile = useState<string | null>("reel:currentFile", () => null);
  const error = ref<string | null>(null);

  const cast = async (relativePath: string) => {
    casting.value = true;
    error.value = null;

    try {
      await rivetCast({ relativePath });
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

  const stop = async () => {
    error.value = null;

    try {
      await castStop();
      currentFile.value = null;
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Stop failed";
    }
  };

  return { casting, currentFile, error, cast, stop };
};
