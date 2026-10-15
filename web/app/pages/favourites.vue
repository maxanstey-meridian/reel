<script setup lang="ts">
import { findNodeByPath } from "~~/app/features/library/logic/library";
import type { FileNode } from "~~/app/lib/api";
import { injectFavourites } from "~~/app/ports/favourites";

const { favourites, loading, error, refresh } = injectFavourites();
const library = useLibrary();
const refreshAll = async () => {
  await Promise.all([refresh(), library.refresh()]);
};

// A favourite missing from the tree (library unavailable, or the file moved) still shows by path.
const favouriteNodes = computed(() =>
  favourites.value.map(
    (fav): FileNode =>
      findNodeByPath(library.tree.value, fav.relativePath) ?? {
        name: fav.relativePath.split("/").pop() ?? fav.relativePath,
        type: "file",
        relativePath: fav.relativePath,
      },
  ),
);
</script>

<template>
  <div class="space-y-4">
    <PullToRefresh :refresh="refreshAll" />

    <h1 class="page-title">Favourites</h1>

    <div v-if="loading || library.loading.value" class="media-grid">
      <USkeleton v-for="i in 6" :key="i" class="h-48" />
    </div>

    <div v-else-if="error && favourites.length === 0" class="space-y-4">
      <UAlert color="error" icon="i-lucide-alert-circle" :title="error.message" />
      <UButton block @click="refresh()">Retry</UButton>
    </div>

    <div
      v-else-if="favouriteNodes.length === 0"
      class="flex flex-col items-center justify-center py-16 text-neutral-500"
    >
      <UIcon name="i-lucide-heart" class="size-12 mb-3" />
      <p class="text-sm font-medium">No favourites yet</p>
      <p class="text-xs text-neutral-600 mt-1">Browse your library and tap the heart icon</p>
    </div>

    <template v-else>
      <UAlert
        v-if="library.error.value"
        color="warning"
        variant="soft"
        title="Library details unavailable"
        :description="library.error.value.message"
      />
      <div class="media-grid">
        <MediaCard v-for="node in favouriteNodes" :key="node.relativePath" :node="node" />
      </div>
    </template>
  </div>
</template>
