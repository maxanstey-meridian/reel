<script setup lang="ts">
import { findNodeByPath } from "~~/app/features/library/logic/library";
import { hasIncompleteProgress, progressFraction } from "~~/app/features/playback/logic/playback";

const library = useLibrary();
const progress = useProgress();
const loading = computed(() => library.loading.value || progress.status.value === "pending");
const error = computed(() => library.error.value?.message ?? progress.error.value?.message);
const items = computed(() =>
  progress.data.value.filter(hasIncompleteProgress).flatMap((entry) => {
    const node = findNodeByPath(library.tree.value, entry.relativePath);
    return node ? [{ node, progress: progressFraction(entry) }] : [];
  }),
);
const refresh = async () => {
  await Promise.all([library.refresh(), progress.refresh()]);
};
</script>

<template>
  <div class="space-y-4">
    <PullToRefresh :refresh="refresh" />

    <h1 class="page-title">Continue Watching</h1>
    <UAlert v-if="error" color="warning" :title="error" />
    <div v-if="loading && items.length === 0" class="media-grid">
      <USkeleton v-for="i in 6" :key="i" class="h-48" />
    </div>
    <div v-else-if="items.length" class="media-grid">
      <MediaCard
        v-for="item in items"
        :key="item.node.relativePath"
        :node="item.node"
        :progress="item.progress"
      />
    </div>
    <p v-else-if="!error" class="py-12 text-center text-sm text-neutral-400">
      Nothing to resume yet. Pick something from your library.
    </p>
    <UButton v-if="error" variant="ghost" @click="refresh">Retry</UButton>
  </div>
</template>
