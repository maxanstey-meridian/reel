<script setup lang="ts">
import { countFilesRecursive, mediaLink } from "~~/app/features/library/logic/library";
import type { FileNode } from "~~/app/lib/api";
import { injectFavourites } from "~~/app/ports/favourites";
import { injectTags } from "~~/app/ports/tags";

const props = defineProps<{
  node: FileNode;
  progress?: number;
}>();

const { isFavourite, toggle } = injectFavourites();
const { forPath: tagsForPath } = injectTags();

const favourited = computed(() => isFavourite(props.node.relativePath));
const fileCount = computed(() =>
  props.node.type === "directory" ? countFilesRecursive(props.node) : null,
);

const formatSize = (bytes: number): string => {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(0)} KB`;
  }
  if (bytes < 1024 * 1024 * 1024) {
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
};
</script>

<template>
  <NuxtLink
    :to="mediaLink(node)"
    class="min-w-0 select-none flex flex-col rounded bg-neutral-900 overflow-hidden text-left transition-all hover:ring-1 hover:ring-green-500 focus-visible:ring-1 focus-visible:ring-green-500 outline-none"
  >
    <div
      class="relative h-[120px] flex items-center justify-center bg-neutral-800/50 text-neutral-500 overflow-hidden"
    >
      <MediaThumbnail :node="node" />

      <div
        v-if="progress != null && progress > 0"
        class="absolute bottom-0 left-0 right-0 h-1 bg-neutral-700"
      >
        <div class="h-full bg-green-500" :style="{ width: `${Math.min(progress * 100, 100)}%` }" />
      </div>

      <button
        v-if="node.type === 'file'"
        :aria-label="favourited ? 'Remove from favourites' : 'Add to favourites'"
        class="absolute top-1 right-1 inline-flex size-9 items-center justify-center rounded-full transition-colors hover:bg-white/5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-500"
        :class="favourited ? 'text-red-500' : 'text-neutral-400 hover:text-red-400'"
        @click.prevent.stop="toggle(node.relativePath)"
      >
        <UIcon name="i-lucide-heart" class="size-5" :class="favourited ? 'fill-current' : ''" />
      </button>
    </div>

    <div class="p-2 flex flex-1 flex-col gap-1 min-h-[72px]">
      <span class="text-xs font-medium leading-tight line-clamp-2">
        {{ node.parsedTitle ?? node.name }}
      </span>
      <span v-if="node.year" class="text-xs text-neutral-500">
        {{ node.year }}
      </span>
      <span v-if="fileCount != null" class="text-[10px] text-neutral-500">
        {{ fileCount }} {{ fileCount === 1 ? "file" : "files" }}
      </span>

      <div class="flex flex-wrap gap-1 text-[10px] text-neutral-400">
        <span v-for="tag in tagsForPath(node.relativePath)" :key="tag">#{{ tag }}</span>
      </div>
      <div class="flex flex-wrap items-center gap-1 mt-auto">
        <UBadge
          v-if="node.extension && node.type === 'file'"
          size="xs"
          color="neutral"
          variant="subtle"
        >
          {{ node.extension }}
        </UBadge>
        <span v-if="node.sizeBytes && node.type === 'file'" class="text-[10px] text-neutral-500">
          {{ formatSize(node.sizeBytes) }}
        </span>
      </div>
    </div>
  </NuxtLink>
</template>
