<script setup lang="ts">
import type { FileNode } from "~~/generated/rivet/types/domain";
import { useConnection } from "../composables/useConnection";
import { useLibrary } from "../composables/useLibrary";
import { useProgress } from "../composables/useProgress";
import { usePullToRefresh } from "../composables/usePullToRefresh";
import { useScrollPreservation } from "../composables/useScrollPreservation";
import type { WatchProgress } from "../composables/useProgress";
import { getNameInitial, scrollToNameInitial } from "../utils/alphabetScroller";
import { findNodeByPath } from "../utils/findNodeByPath";

const { isConnected } = useConnection();
const {
  tree,
  currentChildren,
  breadcrumbs,
  currentPath,
  loading,
  error,
  enterDirectory,
  navigateUp,
  refresh,
} = useLibrary();

const { entries: progressEntries, fetch: fetchProgress } = useProgress();

useScrollPreservation(loading, currentPath);

const { refreshing, pullOffset } = usePullToRefresh(() => refresh());

const continueWatchingItems = computed(() => {
  if (currentPath.value.length > 0) {
    return [];
  }
  return progressEntries.value
    .map((entry: WatchProgress) => {
      const node = findNodeByPath(tree.value, entry.relativePath);
      const progress =
        entry.durationSeconds > 0 ? entry.positionSeconds / entry.durationSeconds : 0;
      return node ? { node, progress } : null;
    })
    .filter((item): item is { node: FileNode; progress: number } => item !== null);
});

onMounted(() => {
  if (!isConnected.value) {
    navigateTo("/connect", { replace: true });
    return;
  }
  fetchProgress();
});

const directories = computed(() =>
  currentChildren.value.filter((n: FileNode) => n.type === "directory"),
);

const files = computed(() => currentChildren.value.filter((n: FileNode) => n.type === "file"));

// Navigation level = mostly directories, few/no loose files.
// Show a clickable folder grid instead of expanding every dir into a MediaRow.
const isNavigationLevel = computed(() => directories.value.length > 0 && files.value.length === 0);

const handleSelect = (node: FileNode) => {
  if (node.type === "directory") {
    enterDirectory(node.name);
  } else {
    navigateTo(`/detail/${encodeURIComponent(node.relativePath)}`);
  }
};

const handleRowSelect = (parentDir: FileNode, node: FileNode) => {
  if (node.type === "file") {
    navigateTo(`/detail/${encodeURIComponent(node.relativePath)}`);
  } else {
    enterDirectory(parentDir.name);
    if (node.type === "directory") {
      enterDirectory(node.name);
    }
  }
};

const childNames = computed(() => currentChildren.value.map((n: FileNode) => n.name));

const handleScrollToLetter = (letter: string) => {
  scrollToNameInitial(letter);
};
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

    <!-- Header -->
    <div class="page-header">
      <h1 class="page-title">Library</h1>
    </div>

    <!-- Breadcrumbs -->
    <div v-if="currentPath.length > 0" class="flex items-center gap-1 text-sm">
      <button class="text-neutral-400 hover:text-white transition-colors" @click="currentPath = []">
        Library
      </button>
      <template v-for="crumb in breadcrumbs" :key="crumb.name">
        <UIcon name="i-lucide-chevron-right" class="size-3 text-neutral-600" />
        <button
          class="text-neutral-400 hover:text-white transition-colors last:text-white"
          @click="currentPath = crumb.path"
        >
          {{ crumb.name }}
        </button>
      </template>
    </div>

    <!-- Back button -->
    <UButton
      v-if="currentPath.length > 0"
      variant="ghost"
      color="neutral"
      size="sm"
      icon="i-lucide-arrow-left"
      @click="navigateUp()"
    >
      Back
    </UButton>

    <!-- Loading -->
    <div v-if="loading" class="space-y-6">
      <div v-for="i in 3" :key="i" class="space-y-2">
        <USkeleton class="h-4 w-24" />
        <div class="flex gap-3">
          <USkeleton v-for="j in 3" :key="j" class="h-[192px] w-[140px] shrink-0" />
        </div>
      </div>
    </div>

    <!-- Error -->
    <div v-else-if="error" class="space-y-4">
      <UAlert color="error" icon="i-lucide-alert-circle" :title="error" />
      <UButton block @click="refresh()">Retry</UButton>
    </div>

    <!-- Content -->
    <div v-else class="space-y-6">
      <!-- Continue Watching row -->
      <div v-if="continueWatchingItems.length > 0" class="space-y-2">
        <h3 class="text-sm font-medium text-neutral-400">Continue Watching</h3>
        <div class="flex gap-3 overflow-x-auto pb-2 scrollbar-hide">
          <MediaCard
            v-for="item in continueWatchingItems"
            :key="item.node.relativePath"
            :node="item.node"
            :progress="item.progress"
            @select="handleSelect(item.node)"
          />
        </div>
      </div>

      <!-- Navigation level: folder grid -->
      <div v-if="isNavigationLevel" class="grid grid-cols-2 gap-3 pr-6 [&>button]:w-full">
        <MediaCard
          v-for="dir in directories"
          :key="dir.relativePath"
          :node="dir"
          :data-name-initial="getNameInitial(dir.name)"
          @select="enterDirectory(dir.name)"
        />
      </div>

      <!-- Content level: directory rows expanded -->
      <MediaRow
        v-for="dir in isNavigationLevel ? [] : directories"
        :key="dir.relativePath"
        :title="dir.name"
        :items="dir.children ?? []"
        @select="(node: FileNode) => handleRowSelect(dir, node)"
      />

      <!-- Loose files grid -->
      <div v-if="files.length > 0" class="space-y-2">
        <h3 v-if="directories.length > 0" class="text-sm font-medium text-neutral-400">Files</h3>
        <div class="grid grid-cols-2 gap-3 pr-6 [&>button]:w-full">
          <MediaCard
            v-for="file in files"
            :key="file.relativePath"
            :node="file"
            :data-name-initial="getNameInitial(file.name)"
            @select="handleSelect(file)"
          />
        </div>
      </div>

      <!-- Empty -->
      <div
        v-if="currentChildren.length === 0 && !loading"
        class="flex flex-col items-center justify-center py-12 text-neutral-500"
      >
        <UIcon name="i-lucide-folder-open" class="size-12 mb-3" />
        <p class="text-sm">This folder is empty</p>
      </div>

      <!-- Alphabet scroller -->
      <AlphabetScroller :names="childNames" @scroll-to="handleScrollToLetter" />
    </div>

  </div>
</template>
