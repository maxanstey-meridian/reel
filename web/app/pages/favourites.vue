<script setup lang="ts">
import type { FileNode } from "~~/generated/rivet/types/domain";
import { useConnection } from "../composables/useConnection";
import { useFavourites } from "../composables/useFavourites";
import { useLibrary } from "../composables/useLibrary";
import { findNodeByPath } from "../utils/findNodeByPath";

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

const favouriteNodes = computed(() =>
  favourites.value
    .map((fav) => findNodeByPath(tree.value, fav.relativePath))
    .filter((node): node is FileNode => node !== null),
);
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
      <p class="text-xs text-neutral-600 mt-1">Browse your library and tap the heart icon</p>
    </div>

    <!-- Favourites grid -->
    <div v-else class="grid grid-cols-2 gap-3">
      <MediaCard
        v-for="node in favouriteNodes"
        :key="node.relativePath"
        :node="node"
        @select="navigateTo(`/detail/${encodeURIComponent(node.relativePath)}`)"
      />
    </div>

  </div>
</template>
