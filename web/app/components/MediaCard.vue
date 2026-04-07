<script setup lang="ts">
import { computed, ref } from "vue";
import type { FileNode } from "~~/generated/rivet/types/models";
import { parseFilename } from "~~/app/utils/parseFilename";
import { useAuthImage } from "~~/app/composables/useAuthImage";

const props = defineProps<{ node: FileNode }>();
defineEmits<{ select: [node: FileNode] }>();

const parsed = computed(() =>
  props.node.type === "File" ? parseFilename(props.node.name) : null,
);

const displayName = computed(() =>
  parsed.value ? parsed.value.title : props.node.name,
);

const thumbnailPath = computed(() => props.node.thumbnailUrl ?? null);
const { src: thumbnailSrc, error: thumbnailError } =
  useAuthImage(thumbnailPath);

const imgFailed = ref(false);
const showImage = computed(
  () => thumbnailSrc.value && !imgFailed.value && !thumbnailError.value,
);

const VIDEO_EXTENSIONS = new Set([
  ".mp4",
  ".mkv",
  ".avi",
  ".mov",
  ".wmv",
  ".flv",
  ".webm",
  ".m4v",
]);

const iconFor = (node: FileNode) => {
  if (node.type === "Directory") return "i-lucide-folder";
  if (node.extension && VIDEO_EXTENSIONS.has(node.extension.toLowerCase()))
    return "i-lucide-film";
  return "i-lucide-file";
};

const formatSize = (bytes: number | null): string => {
  if (bytes === null) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  if (bytes < 1024 * 1024 * 1024)
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
};
</script>

<template>
  <button
    class="w-[140px] shrink-0 flex flex-col rounded bg-neutral-900 overflow-hidden text-left transition-all hover:ring-1 hover:ring-green-500 focus-visible:ring-1 focus-visible:ring-green-500 outline-none"
    @click="$emit('select', node)"
  >
    <div
      class="h-[120px] flex items-center justify-center bg-neutral-800/50 text-neutral-500 overflow-hidden"
    >
      <img
        v-if="showImage"
        :src="thumbnailSrc!"
        loading="lazy"
        class="w-full h-full object-cover"
        @error="imgFailed = true"
      >
      <UIcon v-else :name="iconFor(node)" class="size-10" />
    </div>

    <div class="p-2 flex flex-col gap-1 min-h-[72px]">
      <span class="text-xs font-medium leading-tight line-clamp-2">
        {{ displayName }}
      </span>
      <span
        v-if="parsed?.year"
        class="text-xs text-neutral-500"
      >
        {{ parsed.year }}
      </span>

      <div class="flex items-center gap-1 mt-auto">
        <UBadge
          v-if="node.extension && node.type === 'File'"
          size="xs"
          color="neutral"
          variant="subtle"
        >
          {{ node.extension }}
        </UBadge>
        <span
          v-if="node.sizeBytes && node.type === 'File'"
          class="text-[10px] text-neutral-500"
        >
          {{ formatSize(node.sizeBytes) }}
        </span>
      </div>
    </div>
  </button>
</template>
