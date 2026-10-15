<script setup lang="ts">
import {
  countFilesRecursive,
  formatFileSize,
  mediaLink,
} from "~~/app/features/library/logic/library";
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

      <UButton
        v-if="node.type === 'file'"
        icon="i-lucide-heart"
        variant="ghost"
        size="lg"
        :color="favourited ? 'error' : 'neutral'"
        :aria-label="favourited ? 'Remove from favourites' : 'Add to favourites'"
        :ui="{ leadingIcon: favourited ? 'fill-current' : '' }"
        class="absolute top-1 right-1 rounded-full"
        @click.prevent.stop="toggle(node.relativePath)"
      />
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
        <span v-for="tag in tagsForPath(node.relativePath, node.type)" :key="tag">#{{ tag }}</span>
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
          {{ formatFileSize(node.sizeBytes) }}
        </span>
      </div>
    </div>
  </NuxtLink>
</template>
