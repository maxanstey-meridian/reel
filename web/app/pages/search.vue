<script setup lang="ts">
const RECENT_SEARCHES_KEY = "reel:recentSearches";
const MAX_RECENT = 5;

const query = ref("");
const hasSearched = ref(false);
const recentSearches = ref<string[]>([]);
const searchInput = ref<{ $el: HTMLElement } | null>(null);
const { results, loading, error, completedQuery, search, debouncedSearch } = useSearch();
const { refreshing, pullOffset } = usePullToRefresh(() => search(query.value));

watch(query, (value) => {
  if (value.trim()) {
    hasSearched.value = true;
  }
  debouncedSearch(value);
});

watch(completedQuery, (trimmed) => {
  if (results.value.length > 0 && trimmed) {
    recentSearches.value = [trimmed, ...recentSearches.value.filter((term) => term !== trimmed)].slice(
      0,
      MAX_RECENT,
    );
    localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(recentSearches.value));
  }
});

const clearSearch = () => {
  query.value = "";
  hasSearched.value = false;
  searchInput.value?.$el?.querySelector("input")?.focus();
};

const clearHistory = () => {
  recentSearches.value = [];
  localStorage.removeItem(RECENT_SEARCHES_KEY);
};

const selectRecent = (term: string) => {
  query.value = term;
};

onMounted(() => {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(RECENT_SEARCHES_KEY) ?? "[]");
    if (Array.isArray(parsed) && parsed.every((value) => typeof value === "string")) {
      recentSearches.value = parsed;
    }
  } catch {
    recentSearches.value = [];
  }
});
</script>

<template>
  <div class="space-y-4">
    <div
      v-if="refreshing || pullOffset > 0"
      class="flex justify-center overflow-hidden transition-all"
      :style="{ height: refreshing ? '32px' : `${Math.min(pullOffset, 60)}px` }"
    >
      <UIcon
        :name="refreshing ? 'i-lucide-loader-2' : 'i-lucide-arrow-down'"
        class="size-5 text-neutral-500"
        :class="{ 'animate-spin': refreshing }"
      />
    </div>

    <UInput ref="searchInput" v-model="query" icon="i-lucide-search" placeholder="Search by title..." size="lg" autofocus>
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

    <div v-if="loading" class="flex justify-center py-12">
      <UIcon name="i-lucide-loader-2" class="size-6 text-neutral-500 animate-spin" />
    </div>
    <UAlert v-else-if="error" color="error" icon="i-lucide-alert-circle" :title="error" />
    <div v-else-if="results.length > 0" class="grid grid-cols-2 gap-3 [&>button]:w-full">
      <MediaCard
        v-for="node in results"
        :key="node.relativePath"
        :node="node"
        @select="navigateTo(`/detail/${encodeURIComponent(node.relativePath)}`)"
      />
    </div>
    <div
      v-else-if="query.trim().length > 0 && hasSearched"
      class="flex flex-col items-center justify-center py-12 text-neutral-500"
    >
      <UIcon name="i-lucide-search-x" class="size-12 mb-3" />
      <p class="text-sm">No results found for '{{ query.trim() }}'</p>
    </div>
    <div v-else-if="recentSearches.length > 0" class="space-y-3">
      <p class="text-xs font-medium text-neutral-500 uppercase tracking-wide">Recent</p>
      <div class="flex flex-wrap gap-2">
        <UButton
          v-for="term in recentSearches"
          :key="term"
          :label="term"
          size="sm"
          color="neutral"
          variant="subtle"
          icon="i-lucide-clock"
          @click="selectRecent(term)"
        />
      </div>
      <button class="text-xs text-neutral-500 hover:text-neutral-400" @click="clearHistory">Clear history</button>
    </div>
    <div v-else class="flex flex-col items-center justify-center py-20 text-neutral-500">
      <UIcon name="i-lucide-search" class="size-16 mb-4 text-neutral-700" />
      <p class="text-sm">Search by title</p>
    </div>
  </div>
</template>
