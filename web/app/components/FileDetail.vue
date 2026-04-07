<script setup lang="ts">
import type { FileNode } from "~~/generated/rivet/types/models";
import { parseFilename } from "~~/app/utils/parseFilename";
import { useCast } from "~~/app/composables/useCast";
import { useConnection } from "~~/app/composables/useConnection";
import { computed, ref, watch } from "vue";

interface MetadataResponse {
  tmdbId: number;
  posterUrl: string | null;
  backdropUrl: string | null;
  overview: string | null;
  releaseDate: string | null;
}

const props = defineProps<{
  node: FileNode | null;
  open: boolean;
}>();

const emit = defineEmits<{ close: [] }>();

const { casting, error, cast } = useCast();
const { serverUrl, secretKey } = useConnection();

const parsed = computed(() =>
  props.node ? parseFilename(props.node.name) : null,
);

const metadata = ref<MetadataResponse | null>(null);
const metadataLoading = ref(false);

const fetchMetadata = async (node: FileNode) => {
  metadata.value = null;
  metadataLoading.value = true;
  try {
    const url = `${serverUrl.value}/api/files/metadata?path=${encodeURIComponent(node.relativePath)}`;
    const response = await fetch(url, {
      headers: { "X-Secret-Key": secretKey.value },
    });
    if (response.ok) {
      metadata.value = await response.json();
    }
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
      <!-- Poster -->
      <div v-if="metadata?.posterUrl" class="flex justify-center">
        <img
          :src="metadata.posterUrl"
          loading="lazy"
          class="rounded max-h-64 object-contain"
          alt="Movie poster"
        />
      </div>

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

      <!-- Overview from TMDB -->
      <p
        v-if="metadata?.overview"
        class="text-sm text-neutral-400 leading-relaxed"
      >
        {{ metadata.overview }}
      </p>

      <!-- Loading metadata -->
      <div
        v-if="metadataLoading"
        class="flex items-center gap-2 text-sm text-neutral-500"
      >
        <UIcon name="i-lucide-loader-2" class="size-4 animate-spin" />
        Loading metadata...
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
