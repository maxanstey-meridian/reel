<script setup lang="ts">
import { useLocalStorage } from "@vueuse/core";

const { query, term, results, searching, error, refresh, rescan, rescanning } = useSearch();
const recentSearches = useLocalStorage<string[]>("reel:recentSearches", []);
const searchInput = useTemplateRef("searchInput");

watch(results, (found) => {
  if (found.length > 0 && term.value) {
    recentSearches.value = [
      term.value,
      ...recentSearches.value.filter((t) => t !== term.value),
    ].slice(0, 5);
  }
});

const clearSearch = () => {
  query.value = "";
  searchInput.value?.inputRef?.focus();
};
</script>

<template>
  <div class="space-y-4">
    <PullToRefresh :refresh="refresh" />

    <UInput
      ref="searchInput"
      v-model="query"
      icon="i-lucide-search"
      placeholder="Search titles or #tags..."
      size="lg"
      autofocus
    >
      <template #trailing>
        <UButton
          v-if="query.length > 0"
          icon="i-lucide-x"
          color="neutral"
          variant="link"
          size="xs"
          @mousedown.prevent="clearSearch"
        />
      </template>
    </UInput>

    <template v-if="!query.trim()">
      <div v-if="recentSearches.length > 0" class="space-y-3">
        <p class="text-xs font-medium text-neutral-500 uppercase tracking-wide">Recent</p>
        <div class="flex flex-wrap gap-2">
          <UButton
            v-for="recent in recentSearches"
            :key="recent"
            :label="recent"
            size="sm"
            color="neutral"
            variant="subtle"
            icon="i-lucide-clock"
            @click="void (query = recent)"
          />
        </div>
        <button
          class="text-xs text-neutral-500 hover:text-neutral-400"
          @click="recentSearches = []"
        >
          Clear history
        </button>
      </div>
      <div v-else class="flex flex-col items-center justify-center py-20 text-neutral-500">
        <UIcon name="i-lucide-search" class="size-16 mb-4 text-neutral-700" />
        <p class="text-sm">Search by title</p>
      </div>
    </template>
    <div v-else-if="searching" class="flex justify-center py-12">
      <UIcon name="i-lucide-loader-2" class="size-6 text-neutral-500 animate-spin" />
    </div>
    <UAlert v-else-if="error" color="error" icon="i-lucide-alert-circle" :title="error.message" />
    <template v-else>
      <div v-if="results.length > 0" class="media-grid">
        <MediaCard v-for="node in results" :key="node.relativePath" :node="node" />
      </div>
      <div v-else class="flex flex-col items-center justify-center py-12 text-neutral-500">
        <UIcon name="i-lucide-search-x" class="size-12 mb-3" />
        <p class="text-sm">No results found for '{{ term }}'</p>
      </div>
      <UButton
        block
        variant="ghost"
        color="neutral"
        size="sm"
        icon="i-lucide-folder-sync"
        :loading="rescanning"
        @click="rescan"
      >
        Look for new files
      </UButton>
    </template>
  </div>
</template>
