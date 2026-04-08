<script setup lang="ts">
import { ref, watch } from "vue";
import type { FileNode } from "~~/generated/rivet/types/domain";
import { useSearch } from "../composables/useSearch";

const query = ref("");
const { results, loading, error, debouncedSearch } = useSearch();

watch(query, (q) => {
  debouncedSearch(q);
});

const selectedFile = ref<FileNode | null>(null);
const detailOpen = ref(false);

const openDetail = (node: FileNode) => {
  selectedFile.value = node;
  detailOpen.value = true;
};
</script>

<template>
  <div class="space-y-4">
    <div class="page-header">
      <h1 class="page-title">Search</h1>
    </div>

    <UInput
      v-model="query"
      icon="i-lucide-search"
      placeholder="Search by title, tag, genre..."
      size="lg"
      autofocus
    />

    <!-- Loading -->
    <div v-if="loading" class="flex justify-center py-12">
      <UIcon name="i-lucide-loader-2" class="size-6 text-neutral-500 animate-spin" />
    </div>

    <!-- Error -->
    <UAlert v-else-if="error" color="error" icon="i-lucide-alert-circle" :title="error" />

    <!-- Results -->
    <div v-else-if="results.length > 0" class="grid grid-cols-2 gap-3 [&>button]:w-full">
      <MediaCard
        v-for="node in results"
        :key="node.relativePath"
        :node="node"
        @select="openDetail(node)"
      />
    </div>

    <!-- Empty state -->
    <div
      v-else-if="query.trim().length > 0 && !loading"
      class="flex flex-col items-center justify-center py-12 text-neutral-500"
    >
      <UIcon name="i-lucide-search-x" class="size-12 mb-3" />
      <p class="text-sm">No results found</p>
    </div>

    <FileDetail :node="selectedFile" :open="detailOpen" @close="detailOpen = false" />
  </div>
</template>
