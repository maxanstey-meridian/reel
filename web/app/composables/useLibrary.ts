import { library as fetchLibrary } from "~~/generated/rivet/client/reel";
import type { FileNode } from "~~/generated/rivet/types/domain";

export const useLibrary = () => {
  const tree = ref<FileNode | null>(null);
  const currentPath = useState<string[]>("library:currentPath", () => []);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const currentChildren = computed<FileNode[]>(() => {
    if (!tree.value?.children) {
      return [];
    }

    let node = tree.value;
    for (const segment of currentPath.value) {
      const child = node.children?.find((c) => c.name === segment && c.type === "directory");
      if (!child?.children) {
        return [];
      }
      node = child;
    }

    return node.children ?? [];
  });

  const breadcrumbs = computed(() =>
    currentPath.value.map((name, i) => ({
      name,
      path: currentPath.value.slice(0, i + 1),
    })),
  );

  const enterDirectory = (name: string) => {
    currentPath.value = [...currentPath.value, name];
  };

  const navigateUp = () => {
    if (currentPath.value.length === 0) {
      return;
    }
    currentPath.value = currentPath.value.slice(0, -1);
  };

  const refresh = async () => {
    loading.value = true;
    error.value = null;
    try {
      tree.value = await fetchLibrary();
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Failed to load library";
    } finally {
      loading.value = false;
    }
  };

  refresh();

  return {
    tree,
    currentPath,
    currentChildren,
    breadcrumbs,
    loading,
    error,
    enterDirectory,
    navigateUp,
    refresh,
  };
};
