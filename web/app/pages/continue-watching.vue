<script setup lang="ts">
import { continueWatching } from "~~/app/features/playback/logic/playback";

const library = useLibrary();
const progress = useProgress();
// Wait for the tree too, so titles don't flash as file names on the first visit.
const loading = computed(
  () =>
    library.loading.value ||
    (progress.status.value === "pending" && progress.data.value.length === 0),
);
const error = computed(() => library.error.value?.message ?? progress.error.value?.message);
const items = computed(() => continueWatching(progress.data.value, library.tree.value));
const refresh = async () => {
  await Promise.all([library.refresh(), progress.refresh()]);
};
</script>

<template>
  <div class="space-y-4">
    <PullToRefresh :refresh="refresh" />

    <h1 class="page-title">Continue Watching</h1>
    <div v-if="error" class="space-y-4">
      <UAlert color="warning" :title="error" />
      <UButton block @click="refresh">Retry</UButton>
    </div>
    <div v-if="loading" class="media-grid">
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
  </div>
</template>
