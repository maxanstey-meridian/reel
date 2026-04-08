<script setup lang="ts">
import { ref, watch, onMounted, computed } from "vue";
import { usePullToRefresh } from "../composables/usePullToRefresh";
import { useSearch } from "../composables/useSearch";
import { useTags, type TagCount } from "../composables/useTags";

const RECENT_SEARCHES_KEY = "reel:recentSearches";
const MAX_RECENT = 5;

const query = ref("");
const hasSearched = ref(false);
const activeTag = ref<string | null>(null);
const watchedFilter = ref<boolean | null>(null);
const allTags = ref<TagCount[]>([]);
const tagPopoverOpen = ref(false);
const recentSearches = ref<string[]>([]);

const { results, loading, error, search, debouncedSearch } = useSearch();

const { refreshing, pullOffset } = usePullToRefresh(() =>
  search(query.value, activeTag.value, null, watchedFilter.value),
);
const { getAllTags } = useTags();

const searchInput = ref<{ $el: HTMLElement } | null>(null);

const triggerSearch = () => {
  const q = query.value;
  if (q.trim() || activeTag.value || watchedFilter.value !== null) {
    hasSearched.value = true;
  }
  debouncedSearch(q, activeTag.value, null, watchedFilter.value);
};

watch(query, triggerSearch);
watch(activeTag, triggerSearch);
watch(watchedFilter, triggerSearch);

watch(results, (newResults) => {
  const trimmed = query.value.trim();
  if (newResults.length > 0 && trimmed) {
    const filtered = recentSearches.value.filter((s) => s !== trimmed);
    recentSearches.value = [trimmed, ...filtered].slice(0, MAX_RECENT);
    localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(recentSearches.value));
  }
});

const clearSearch = () => {
  query.value = "";
  hasSearched.value = false;
  activeTag.value = null;
  watchedFilter.value = null;
  searchInput.value?.$el?.querySelector("input")?.focus();
};

const selectRecent = (term: string) => {
  query.value = term;
};

const clearHistory = () => {
  recentSearches.value = [];
  localStorage.removeItem(RECENT_SEARCHES_KEY);
};

const clearTag = () => {
  activeTag.value = null;
};

const selectTag = (tag: string) => {
  activeTag.value = tag;
  tagPopoverOpen.value = false;
};

const cycleWatchedFilter = () => {
  if (watchedFilter.value === null) {
    watchedFilter.value = false;
  } else if (watchedFilter.value === false) {
    watchedFilter.value = true;
  } else {
    watchedFilter.value = null;
  }
};

const watchedLabel = computed(() => {
  if (watchedFilter.value === null) return "Watched";
  if (watchedFilter.value === false) return "Unwatched";
  return "Watched";
});

const watchedIcon = computed(() => {
  if (watchedFilter.value === null) return "i-lucide-eye";
  if (watchedFilter.value === false) return "i-lucide-eye-off";
  return "i-lucide-eye";
});

const hasActiveFilters = computed(() => activeTag.value !== null || watchedFilter.value !== null);

const noResultsMessage = computed(() => {
  const parts: string[] = [];
  if (query.value.trim()) {
    parts.push(`for '${query.value.trim()}'`);
  }
  if (activeTag.value) {
    parts.push(`tagged ${activeTag.value}`);
  }
  if (watchedFilter.value === false) {
    parts.push("that are unwatched");
  } else if (watchedFilter.value === true) {
    parts.push("that are watched");
  }
  if (parts.length === 0) return "No results found";
  return `No results found ${parts.join(" ")}`;
});

onMounted(async () => {
  try {
    const stored = localStorage.getItem(RECENT_SEARCHES_KEY);
    if (stored) {
      const parsed = JSON.parse(stored);
      if (Array.isArray(parsed) && parsed.every((x: unknown) => typeof x === "string")) {
        recentSearches.value = parsed;
      }
    }
  } catch {
    // Corrupted localStorage — ignore
  }

  try {
    allTags.value = await getAllTags();
  } catch {
    // Tags are non-critical — fail silently
  }
});
</script>

<template>
  <div class="space-y-4">
    <!-- Pull to refresh indicator -->
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

    <UInput
      ref="searchInput"
      v-model="query"
      icon="i-lucide-search"
      placeholder="Search by title, tag, genre..."
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

    <!-- Filter row -->
    <div class="flex gap-2 overflow-x-auto">
      <!-- Tag filter -->
      <UPopover v-model:open="tagPopoverOpen">
        <UButton
          v-if="!activeTag"
          icon="i-lucide-tag"
          label="Tags"
          size="xs"
          color="neutral"
          variant="subtle"
        />
        <UButton
          v-else
          icon="i-lucide-tag"
          :label="activeTag"
          size="xs"
          color="primary"
          variant="solid"
          trailing-icon="i-lucide-x"
          @click.stop="clearTag"
        />

        <template #content>
          <div class="p-2 max-h-60 overflow-y-auto min-w-48">
            <button
              v-for="tag in allTags"
              :key="tag.tag"
              class="flex w-full items-center justify-between px-3 py-2 text-sm rounded-md hover:bg-neutral-800 text-neutral-200"
              @click="selectTag(tag.tag)"
            >
              <span>{{ tag.tag }}</span>
              <span class="text-neutral-500 text-xs ml-2">{{ tag.count }}</span>
            </button>
            <p v-if="allTags.length === 0" class="px-3 py-2 text-sm text-neutral-500">
              No tags found
            </p>
          </div>
        </template>
      </UPopover>

      <!-- Watched filter -->
      <UButton
        :icon="watchedIcon"
        :label="watchedLabel"
        size="xs"
        :color="watchedFilter !== null ? 'primary' : 'neutral'"
        :variant="watchedFilter !== null ? 'solid' : 'subtle'"
        @click="cycleWatchedFilter"
      />
    </div>

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
      v-else-if="(query.trim().length > 0 || hasActiveFilters) && hasSearched"
      class="flex flex-col items-center justify-center py-12 text-neutral-500"
    >
      <UIcon name="i-lucide-search-x" class="size-12 mb-3" />
      <p class="text-sm">{{ noResultsMessage }}</p>
    </div>

    <!-- Empty state — before any search -->
    <div v-else>
      <!-- Recent searches -->
      <div v-if="recentSearches.length > 0" class="space-y-3">
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
        <button
          class="text-xs text-neutral-500 hover:text-neutral-400"
          @click="clearHistory"
        >
          Clear history
        </button>
      </div>

      <!-- No recent searches -->
      <div
        v-else
        class="flex flex-col items-center justify-center py-20 text-neutral-500"
      >
        <UIcon name="i-lucide-search" class="size-16 mb-4 text-neutral-700" />
        <p class="text-sm">Search by title, tag, or genre</p>
      </div>
    </div>
  </div>
</template>
