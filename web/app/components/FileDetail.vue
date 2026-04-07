<script setup lang="ts">
import type { FileNode } from "~~/generated/rivet/types/models";
import { parseFilename } from "~~/app/utils/parseFilename";
import { useCast } from "~~/app/composables/useCast";
import { computed } from "vue";

const props = defineProps<{
  node: FileNode | null;
  open: boolean;
}>();

const emit = defineEmits<{ close: [] }>();

const { casting, error, cast } = useCast();

const parsed = computed(() =>
  props.node ? parseFilename(props.node.name) : null,
);

const formatSize = (bytes: number | null): string => {
  if (bytes === null) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  if (bytes < 1024 * 1024 * 1024)
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
};

const handleCast = async () => {
  if (!props.node) return;
  await cast(props.node.relativePath);
  if (!error.value) {
    emit("close");
  }
};
</script>

<template>
  <UDrawer
    :open="open"
    handle
    @update:open="(val: boolean) => { if (!val) $emit('close') }"
  >
    <div v-if="node" class="p-6 space-y-4">
      <!-- Title -->
      <div class="space-y-1">
        <h2 class="text-lg font-semibold">
          {{ parsed?.title ?? node.name }}
        </h2>
        <p v-if="parsed?.year" class="text-sm text-neutral-400">
          {{ parsed.year }}
        </p>
      </div>

      <!-- Metadata badges -->
      <div class="flex items-center gap-2">
        <UBadge
          v-if="node.extension"
          size="sm"
          color="neutral"
          variant="subtle"
        >
          {{ node.extension }}
        </UBadge>
        <span v-if="node.sizeBytes" class="text-sm text-neutral-500">
          {{ formatSize(node.sizeBytes) }}
        </span>
      </div>

      <!-- Path -->
      <p class="text-xs text-neutral-600 break-all">
        {{ node.relativePath }}
      </p>

      <!-- Error -->
      <UAlert
        v-if="error"
        color="error"
        icon="i-lucide-alert-circle"
        :title="error"
      />

      <!-- Cast button -->
      <UButton
        block
        size="lg"
        icon="i-lucide-cast"
        :loading="casting"
        @click="handleCast"
      >
        Cast to TV
      </UButton>
    </div>
  </UDrawer>
</template>
