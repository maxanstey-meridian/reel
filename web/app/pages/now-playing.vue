<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { parseFilename } from "~~/app/utils/parseFilename";
import { useCast } from "../composables/useCast";
import { useConnection } from "../composables/useConnection";
import { nextFile, thumbnailUrl } from "~~/generated/rivet/client/reel";
import type { FileNode } from "~~/generated/rivet/types/domain";

const { isConnected, secretKey } = useConnection();
const { currentFile, stop, error, cast, casting, queue, fetchQueue } = useCast();

onMounted(() => {
  if (!isConnected.value) {
    navigateTo("/connect", { replace: true });
  }
});

const filename = computed(() => {
  if (!currentFile.value) {
    return null;
  }
  return currentFile.value.split("/").pop() ?? currentFile.value;
});

const parsed = computed(() => {
  if (!filename.value) {
    return null;
  }
  return parseFilename(filename.value);
});

// Up Next
const upNext = ref<FileNode | null>(null);
const loadingNext = ref(false);

const fetchUpNext = async (path: string) => {
  const requestedPath = path;
  loadingNext.value = true;
  try {
    const result = await nextFile(path, { unwrap: false });
    // Discard stale response if currentFile changed during fetch
    if (currentFile.value !== requestedPath) return;
    if (result.status === 200) {
      upNext.value = result.data;
    } else {
      upNext.value = null;
    }
  } catch {
    upNext.value = null;
  } finally {
    loadingNext.value = false;
  }
};

watch(currentFile, (path) => {
  if (path) {
    fetchUpNext(path);
    fetchQueue();
  } else {
    upNext.value = null;
  }
}, { immediate: true });

const upNextThumb = computed(() =>
  upNext.value?.relativePath && secretKey.value
    ? thumbnailUrl(upNext.value.relativePath, secretKey.value)
    : null,
);

const castNext = () => {
  if (upNext.value) {
    cast(upNext.value.relativePath);
  }
};

// Transport controls (play/pause/seek) are deferred — they require
// bidirectional communication with the TV's ExoPlayer state, which
// needs the TV to report playback state back to the server.
</script>

<template>
  <div class="flex flex-col items-center pt-12">
    <!-- Playing state -->
    <template v-if="currentFile">
      <UIcon name="i-lucide-cast" class="size-12 text-green-500 mb-6" />

      <h1 class="text-xl font-semibold text-center">
        {{ parsed?.title ?? filename }}
      </h1>
      <p v-if="parsed?.year" class="text-sm text-neutral-400 mt-1">
        {{ parsed.year }}
      </p>
      <p class="text-xs text-neutral-600 mt-2 break-all text-center px-4">
        {{ currentFile }}
      </p>

      <UAlert
        v-if="error"
        color="error"
        icon="i-lucide-alert-circle"
        :title="error"
        class="mt-6 w-full"
      />

      <UButton color="error" size="lg" icon="i-lucide-square" class="mt-8 w-full" @click="stop()">
        Stop Casting
      </UButton>

      <!-- Up Next -->
      <div v-if="upNext && queue.length === 0" class="mt-8 w-full">
        <h2 class="text-sm font-medium text-neutral-400 mb-3">Up Next</h2>
        <button
          class="w-full flex items-center gap-3 rounded-lg bg-neutral-900 p-3 text-left transition-all hover:ring-1 hover:ring-green-500 focus-visible:ring-1 focus-visible:ring-green-500 outline-none"
          :disabled="casting"
          @click="castNext"
        >
          <div class="relative size-16 shrink-0 rounded overflow-hidden bg-neutral-800 flex items-center justify-center text-neutral-500">
            <img
              v-if="upNextThumb"
              :src="upNextThumb"
              class="w-full h-full object-cover"
              loading="lazy"
            />
            <UIcon v-else name="i-lucide-film" class="size-6" />
          </div>
          <div class="flex-1 min-w-0">
            <p class="text-sm font-medium truncate">
              {{ upNext.displayTitle ?? upNext.name }}
            </p>
            <p class="text-xs text-neutral-500 mt-0.5">Tap to cast</p>
          </div>
          <UIcon name="i-lucide-cast" class="size-5 text-green-500 shrink-0" />
        </button>
      </div>

      <!-- Queue -->
      <div v-if="queue.length > 0" class="mt-8 w-full">
        <h2 class="text-sm font-medium text-neutral-400 mb-3">
          Queue ({{ queue.length }} remaining)
        </h2>
        <div class="space-y-1">
          <div
            v-for="(path, index) in queue"
            :key="path"
            class="flex items-center gap-3 rounded-lg bg-neutral-900 p-3"
          >
            <span class="text-xs text-neutral-600 w-5 text-right shrink-0">{{ index + 1 }}</span>
            <div class="size-10 rounded bg-neutral-800 overflow-hidden shrink-0 flex items-center justify-center">
              <img
                v-if="secretKey"
                :src="thumbnailUrl(path, secretKey)"
                class="w-full h-full object-cover"
                loading="lazy"
                @error="$event.target instanceof HTMLImageElement && ($event.target.style.display = 'none')"
              />
              <UIcon v-else name="i-lucide-film" class="size-5 text-neutral-500" />
            </div>
            <p class="flex-1 min-w-0 text-sm text-neutral-300 truncate">
              {{ path.split("/").pop() }}
            </p>
          </div>
        </div>
      </div>
    </template>

    <!-- Idle state -->
    <template v-else>
      <UIcon name="i-lucide-cast" class="size-16 text-neutral-700 mb-6" />

      <h1 class="text-xl font-semibold">Nothing playing</h1>
      <p class="text-sm text-neutral-500 mt-2 text-center">Browse your library to cast something</p>

      <UButton
        variant="outline"
        color="neutral"
        size="lg"
        icon="i-lucide-film"
        class="mt-8"
        @click="navigateTo('/library')"
      >
        Browse Library
      </UButton>
    </template>
  </div>
</template>
