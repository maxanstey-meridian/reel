<script setup lang="ts">
import { thumbnailUrl, type FileNode } from "~~/app/lib/api";

const props = defineProps<{ node: FileNode | null }>();
// Thumbnails are frames, so only video has one; a folder shows its first video's.
const firstVideo = (node: FileNode): string | null => {
  if (node.type === "file") {
    return node.kind === "video" ? node.relativePath : null;
  }
  for (const child of node.children ?? []) {
    const found = firstVideo(child);
    if (found) {
      return found;
    }
  }
  return null;
};
const source = computed(() => (props.node ? firstVideo(props.node) : null));
const failed = ref(false);
const loaded = ref(false);
const url = computed(() => (source.value ? thumbnailUrl(source.value) : null));
watch(url, () => {
  failed.value = false;
  loaded.value = false;
});
</script>
<template>
  <div class="relative flex h-full w-full items-center justify-center overflow-hidden">
    <UIcon
      v-if="!loaded"
      :name="node?.type === 'directory' ? 'i-lucide-folder' : 'i-lucide-film'"
      class="size-10"
    />
    <img
      v-if="url && !failed"
      :key="url"
      :src="url"
      alt=""
      loading="lazy"
      decoding="async"
      class="absolute inset-0 h-full w-full object-cover"
      :class="{ 'opacity-0': !loaded }"
      @load="loaded = true"
      @error="
        failed = true;
        loaded = false;
      "
    />
  </div>
</template>
