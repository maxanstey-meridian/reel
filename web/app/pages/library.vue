<script setup lang="ts">
import type { FileNode } from "~~/generated/rivet/types/models";
import { useConnection } from "../composables/useConnection";
import { useLibrary } from "../composables/useLibrary";

const { isConnected } = useConnection();
const {
  currentChildren,
  breadcrumbs,
  currentPath,
  loading,
  error,
  enterDirectory,
  navigateUp,
  refresh,
} = useLibrary();

onMounted(() => {
  if (!isConnected.value) {
    navigateTo("/connect", { replace: true });
  }
});

const directories = computed(() =>
  currentChildren.value.filter((n: FileNode) => n.type === "Directory"),
);

const files = computed(() =>
  currentChildren.value.filter((n: FileNode) => n.type === "File"),
);

const handleSelect = (node: FileNode) => {
  if (node.type === "Directory") {
    enterDirectory(node.name);
  }
};

const handleRowSelect = (parentDir: FileNode, node: FileNode) => {
  enterDirectory(parentDir.name);
  if (node.type === "Directory") {
    enterDirectory(node.name);
  }
};
</script>

<template>
  <div class="space-y-4">
    <!-- Header -->
    <div class="page-header">
      <h1 class="page-title">Library</h1>
    </div>

    <!-- Breadcrumbs -->
    <div
      v-if="currentPath.length > 0"
      class="flex items-center gap-1 text-sm"
    >
      <button
        class="text-neutral-400 hover:text-white transition-colors"
        @click="currentPath = []"
      >
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
      <!-- Directory rows -->
      <MediaRow
        v-for="dir in directories"
        :key="dir.relativePath"
        :title="dir.name"
        :items="dir.children ?? []"
        @select="(node: FileNode) => handleRowSelect(dir, node)"
      />

      <!-- Loose files grid -->
      <div v-if="files.length > 0" class="space-y-2">
        <h3
          v-if="directories.length > 0"
          class="text-sm font-medium text-neutral-400"
        >
          Files
        </h3>
        <div class="grid grid-cols-2 gap-3">
          <MediaCard
            v-for="file in files"
            :key="file.relativePath"
            :node="file"
            @click="handleSelect(file)"
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
    </div>
  </div>
</template>
