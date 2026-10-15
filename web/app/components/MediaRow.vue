<script setup lang="ts">
import type { FileNode } from "~~/app/lib/api";

defineProps<{ title: string; items: FileNode[] }>();
</script>

<template>
  <div class="space-y-2">
    <h3 class="text-sm font-medium text-neutral-400">{{ title }}</h3>

    <div class="media-grid">
      <MediaCard v-for="item in items" :key="item.relativePath" :node="item" />
    </div>
  </div>
</template>
