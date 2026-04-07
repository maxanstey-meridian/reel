<script setup lang="ts">
import type { FileNode } from "~~/generated/rivet/types/models";

defineProps<{ title: string; items: FileNode[] }>();
defineEmits<{ select: [node: FileNode] }>();
</script>

<template>
  <div class="space-y-2">
    <h3 class="text-sm font-medium text-neutral-400">{{ title }}</h3>

    <div class="flex gap-3 overflow-x-auto pb-2 scrollbar-hide">
      <MediaCard
        v-for="item in items"
        :key="item.relativePath"
        :node="item"
        @select="$emit('select', item)"
      />
    </div>
  </div>
</template>
