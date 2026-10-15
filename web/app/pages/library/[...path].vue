<script setup lang="ts">
import { findNodeByPath, libraryLink } from "~~/app/features/library/logic/library";
import { injectTags } from "~~/app/ports/tags";

const route = useRoute();
const { tree, loading, error, refresh, rescan, rescanning } = useLibrary();
const tags = injectTags();
const refreshAll = async () => {
  await Promise.all([refresh(), tags.refresh()]);
};

const segments = computed(() => [route.params.path ?? []].flat().filter(Boolean));
const relativePath = computed(() => segments.value.join("/"));
const children = computed(() => findNodeByPath(tree.value, relativePath.value)?.children ?? []);
const directories = computed(() => children.value.filter((n) => n.type === "directory"));
const files = computed(() => children.value.filter((n) => n.type === "file"));
// A folder of only folders is browsed as a grid rather than expanded into rows.
const isNavigationLevel = computed(() => directories.value.length > 0 && files.value.length === 0);
</script>

<template>
  <div class="space-y-4">
    <PullToRefresh :refresh="refreshAll" />

    <nav aria-label="Breadcrumbs" class="flex flex-wrap items-center gap-1 text-base font-semibold">
      <NuxtLink
        to="/library"
        class="text-neutral-400 hover:text-white transition-colors last:text-white"
        >Library</NuxtLink
      >
      <template v-for="(name, index) in segments" :key="index">
        <UIcon name="i-lucide-chevron-right" class="size-3 shrink-0 text-neutral-600" />
        <NuxtLink
          :to="libraryLink(segments.slice(0, index + 1).join('/'))"
          class="min-w-0 break-words text-neutral-400 hover:text-white transition-colors last:text-white"
        >
          {{ name }}
        </NuxtLink>
      </template>
    </nav>

    <TagEditor v-if="relativePath" :key="relativePath" :path="relativePath" type="directory" />

    <UButton
      v-if="segments.length"
      :to="libraryLink(segments.slice(0, -1).join('/'))"
      variant="ghost"
      color="neutral"
      size="sm"
      icon="i-lucide-arrow-left"
    >
      Back
    </UButton>

    <div v-if="loading" class="media-grid">
      <USkeleton v-for="i in 6" :key="i" class="h-48" />
    </div>

    <div v-else-if="error && !tree" class="space-y-4">
      <UAlert color="error" icon="i-lucide-alert-circle" :title="error.message" />
      <UButton block @click="refresh()">Retry</UButton>
    </div>

    <div v-else class="space-y-6">
      <UAlert
        v-if="error"
        color="warning"
        title="Showing saved library"
        :description="error.message"
      />
      <div v-if="isNavigationLevel" class="media-grid">
        <MediaCard v-for="dir in directories" :key="dir.relativePath" :node="dir" />
      </div>

      <MediaRow
        v-for="dir in isNavigationLevel ? [] : directories"
        :key="dir.relativePath"
        :title="dir.name"
        :items="dir.children ?? []"
      />

      <div v-if="files.length > 0" class="space-y-2">
        <h3 v-if="directories.length > 0" class="text-sm font-medium text-neutral-400">Files</h3>
        <div class="media-grid">
          <MediaCard v-for="file in files" :key="file.relativePath" :node="file" />
        </div>
      </div>

      <div
        v-if="children.length === 0"
        class="flex flex-col items-center justify-center py-12 text-neutral-500"
      >
        <UIcon name="i-lucide-folder-open" class="size-12 mb-3" />
        <p class="text-sm">This folder is empty</p>
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
    </div>
  </div>
</template>
