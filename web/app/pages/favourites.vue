<script setup lang="ts">
import { ref } from "vue";
import type { FileNode } from "~~/generated/rivet/types/models";
import { useConnection } from "../composables/useConnection";
import { useFavourites } from "../composables/useFavourites";
import { useLibrary } from "../composables/useLibrary";

const { isConnected } = useConnection();
const { favourites, loading, fetch: fetchFavourites } = useFavourites();
const { tree } = useLibrary();

onMounted(() => {
  if (!isConnected.value) {
    navigateTo("/connect", { replace: true });
    return;
  }
  fetchFavourites();
});

const findNodeByPath = (
  root: FileNode | null,
  relativePath: string,
): FileNode | null => {
  if (!root) return null;
  if (root.relativePath === relativePath) return root;
  if (!root.children) return null;
  for (const child of root.children) {
    const found = findNodeByPath(child, relativePath);
    if (found) return found;
  }
  return null;
};

const favouriteNodes = computed(() =>
  favourites.value
    .map((fav) => findNodeByPath(tree.value, fav.relativePath))
    .filter((node): node is FileNode => node !== null),
);

const selectedFile = ref<FileNode | null>(null);
const detailOpen = ref(false);

const openDetail = (node: FileNode) => {
  if (node.type === "File") {
    selectedFile.value = node;
    detailOpen.value = true;
  }
};
</script>

<template>
  <div class="space-y-4">
    <div class="page-header">
      <h1 class="page-title">Favourites</h1>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="space-y-3">
      <USkeleton v-for="i in 4" :key="i" class="h-[192px] w-[140px]" />
    </div>

    <!-- Empty state -->
    <div
      v-else-if="favouriteNodes.length === 0"
      class="flex flex-col items-center justify-center py-16 text-neutral-500"
    >
      <UIcon name="i-lucide-heart" class="size-12 mb-3" />
      <p class="text-sm font-medium">No favourites yet</p>
      <p class="text-xs text-neutral-600 mt-1">
        Browse your library and tap the heart icon
      </p>
    </div>

    <!-- Favourites grid -->
    <div v-else class="grid grid-cols-2 gap-3">
      <MediaCard
        v-for="node in favouriteNodes"
        :key="node.relativePath"
        :node="node"
        @select="openDetail(node)"
      />
    </div>

    <!-- File detail drawer -->
    <FileDetail
      :node="selectedFile"
      :open="detailOpen"
      @close="detailOpen = false"
    />
  </div>
</template>
