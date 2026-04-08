<script setup lang="ts">
import { ref, watch } from "vue";
import { useSearch } from "../composables/useSearch";

const query = ref("");
const hasSearched = ref(false);
const { results, loading, error, debouncedSearch } = useSearch();

const searchInput = ref<{ $el: HTMLElement } | null>(null);

watch(query, (q) => {
  if (q.trim()) {
    hasSearched.value = true;
  }
  debouncedSearch(q);
});

const clearSearch = () => {
  query.value = "";
  hasSearched.value = false;
  searchInput.value?.$el?.querySelector("input")?.focus();
};
</script>

<template>
  <div class="space-y-4">
    <UInput
      ref="searchInput"
      v-model="query"
      icon="i-lucide-search"
      placeholder="Search by title, tag, genre..."
      size="lg"
      autofocus
    >
      <template v-if="query.length > 0" #trailing>
        <UButton
          icon="i-lucide-x"
          color="neutral"
          variant="link"
          size="xs"
          @mousedown.prevent="clearSearch"
        />
      </template>
    </UInput>

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
        @select="navigateTo(`/detail/${encodeURIComponent(node.relativePath)}`)"
      />
    </div>

    <!-- No results -->
    <div
      v-else-if="query.trim().length > 0 && hasSearched"
      class="flex flex-col items-center justify-center py-12 text-neutral-500"
    >
      <UIcon name="i-lucide-search-x" class="size-12 mb-3" />
      <p class="text-sm">No results found</p>
    </div>

    <!-- Empty state — before any search -->
    <div
      v-else
      class="flex flex-col items-center justify-center py-20 text-neutral-500"
    >
      <UIcon name="i-lucide-search" class="size-16 mb-4 text-neutral-700" />
      <p class="text-sm">Search by title, tag, or genre</p>
    </div>
  </div>
</template>
