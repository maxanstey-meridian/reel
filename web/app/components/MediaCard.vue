<script setup lang="ts">
import { computed, ref } from "vue";
import { useConnection } from "~~/app/composables/useConnection";
import { useFavourites } from "~~/app/composables/useFavourites";
import { countDirectFiles, findFirstFile } from "~~/app/utils/directoryHelpers";
import { thumbnailUrl } from "~~/generated/rivet/client/reel";
import type { FileNode } from "~~/generated/rivet/types/domain";

const props = defineProps<{
  node: FileNode;
  progress?: number;
}>();
defineEmits<{ select: [node: FileNode] }>();

const { secretKey } = useConnection();
const { isFavourite, toggle } = useFavourites();

const showHeart = computed(() => props.node.type === "file");
const favourited = computed(() => isFavourite(props.node.relativePath));

const displayName = computed(() => props.node.displayTitle ?? props.node.parsedTitle ?? props.node.name);

const isWatched = computed(() => props.node.watched === true);

const isDirectory = computed(() => props.node.type === "directory");

const folderFirstFile = computed(() => (isDirectory.value ? findFirstFile(props.node) : null));

const fileCount = computed(() => (isDirectory.value ? countDirectFiles(props.node) : null));

const thumbnailSrc = computed(() => {
  const file = isDirectory.value ? folderFirstFile.value : props.node;
  if (!file?.relativePath || !secretKey.value) {
    return null;
  }
  return thumbnailUrl(file.relativePath, secretKey.value);
});

const imgFailed = ref(false);
const showImage = computed(() => thumbnailSrc.value && !imgFailed.value);

const VIDEO_EXTENSIONS = new Set([".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v"]);

const iconFor = (node: FileNode) => {
  if (node.type === "directory") {
    return "i-lucide-folder";
  }
  if (node.extension && VIDEO_EXTENSIONS.has(node.extension.toLowerCase())) {
    return "i-lucide-film";
  }
  return "i-lucide-file";
};

const formatSize = (bytes: number | null): string => {
  if (bytes === null) {
    return "";
  }
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
  <button
    class="w-[140px] shrink-0 flex flex-col rounded bg-neutral-900 overflow-hidden text-left transition-all hover:ring-1 hover:ring-green-500 focus-visible:ring-1 focus-visible:ring-green-500 outline-none"
    :class="{ 'opacity-60': isWatched }"
    @click="$emit('select', node)"
  >
    <div
      class="relative h-[120px] flex items-center justify-center bg-neutral-800/50 text-neutral-500 overflow-hidden"
    >
      <img
        v-if="showImage"
        :src="thumbnailSrc ?? ''"
        loading="lazy"
        class="w-full h-full object-cover"
        @error="imgFailed = true"
      />
      <UIcon v-else :name="iconFor(node)" class="size-10" />

      <!-- Progress bar -->
      <div
        v-if="progress != null && progress > 0"
        class="absolute bottom-0 left-0 right-0 h-1 bg-neutral-700"
      >
        <div class="h-full bg-green-500" :style="{ width: `${Math.min(progress * 100, 100)}%` }" />
      </div>

      <!-- Watched indicator -->
      <div
        v-if="isWatched"
        class="absolute bottom-2 left-1 p-1 rounded-full bg-black/50 text-green-500"
      >
        <UIcon name="i-lucide-circle-check" class="size-4" />
      </div>

      <!-- Favourite heart -->
      <button
        v-if="showHeart"
        class="absolute top-1 right-1 p-1 rounded-full bg-black/50 transition-colors"
        :class="favourited ? 'text-red-500' : 'text-neutral-400 hover:text-red-400'"
        @click.stop="toggle(node.relativePath)"
      >
        <UIcon name="i-lucide-heart" class="size-4" :class="favourited ? 'fill-current' : ''" />
      </button>

      <!-- Add to collection -->
      <AddToCollectionPopover
        v-if="showHeart"
        :relative-path="node.relativePath"
        :content="{ side: 'left', align: 'start' }"
      >
        <template #default="{ openPopover }">
          <button
            class="absolute top-8 right-1 p-1 rounded-full bg-black/50 text-neutral-400 hover:text-green-400 transition-colors"
            data-testid="add-to-collection-btn"
            @click.stop="openPopover"
          >
            <UIcon name="i-lucide-list-plus" class="size-4" />
          </button>
        </template>
      </AddToCollectionPopover>
    </div>

    <div class="p-2 flex flex-col gap-1 min-h-[72px]">
      <span class="text-xs font-medium leading-tight line-clamp-2">
        {{ displayName }}
      </span>
      <span v-if="node.year" class="text-xs text-neutral-500">
        {{ node.year }}
      </span>
      <span v-if="fileCount != null" class="text-[10px] text-neutral-500">
        {{ fileCount }} {{ fileCount === 1 ? "file" : "files" }}
      </span>

      <div class="flex items-center gap-1 mt-auto">
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
  </button>
</template>
