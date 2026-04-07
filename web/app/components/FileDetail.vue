<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useCast } from "~~/app/composables/useCast";
import { parseFilename } from "~~/app/utils/parseFilename";
import { metadata as fetchMetadataApi } from "~~/generated/rivet/client/reel";
import type { FileNode } from "~~/generated/rivet/types/domain";
import type { MetadataResponse } from "~~/generated/rivet/types/models";

const props = defineProps<{
  node: FileNode | null;
  open: boolean;
}>();

const emit = defineEmits<{ close: [] }>();

const { casting, error, cast } = useCast();

const parsed = computed(() => (props.node ? parseFilename(props.node.name) : null));

const metadata = ref<MetadataResponse | null>(null);
const metadataLoading = ref(false);

const fetchMetadata = async (node: FileNode) => {
  metadata.value = null;
  metadataLoading.value = true;
  try {
    metadata.value = await fetchMetadataApi(node.relativePath);
  } catch {
    // Metadata is optional — silently ignore
  } finally {
    metadataLoading.value = false;
  }
};

watch(
  () => [props.node, props.open] as const,
  ([node, open]) => {
    if (open && node) {
      fetchMetadata(node);
    }
    if (!open) {
      metadata.value = null;
    }
  },
);

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

const handleCast = async () => {
  if (!props.node) {
    return;
  }
  await cast(props.node.relativePath);
  if (!error.value) {
    emit("close");
  }
};
</script>

<template>
  <USlideover
    :open="open"
    side="bottom"
    :ui="{ content: 'bg-neutral-950 text-white', header: 'bg-neutral-950', body: 'bg-neutral-950' }"
    @update:open="
      (val: boolean) => {
        if (!val) $emit('close');
      }
    "
  >
    <template v-if="node" #header>
      <div class="flex items-center gap-3 w-full">
        <div class="flex-1 min-w-0">
          <h2 class="text-base font-semibold truncate">{{ parsed?.title ?? node.name }}</h2>
          <div class="flex items-center gap-2 mt-0.5">
            <UBadge v-if="node.extension" size="xs" color="neutral" variant="subtle">
              {{ node.extension }}
            </UBadge>
            <span v-if="node.sizeBytes" class="text-xs text-neutral-500">
              {{ formatSize(node.sizeBytes) }}
            </span>
            <span v-if="parsed?.year" class="text-xs text-neutral-500">
              {{ parsed.year }}
            </span>
          </div>
        </div>
      </div>
    </template>

    <template #body>
      <div class="px-6 pb-6 space-y-3">
        <p v-if="metadata?.overview" class="text-sm text-neutral-400 leading-relaxed">
          {{ metadata.overview }}
        </p>

        <UAlert v-if="error" color="error" icon="i-lucide-alert-circle" :title="error" />

        <UButton block size="lg" icon="i-lucide-cast" :loading="casting" @click="handleCast">
          Cast to TV
        </UButton>
      </div>
    </template>
  </USlideover>
</template>
