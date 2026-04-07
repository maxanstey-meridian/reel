<script setup lang="ts">
import { computed } from "vue";
import { parseFilename } from "~~/app/utils/parseFilename";
import { useCast } from "../composables/useCast";
import { useConnection } from "../composables/useConnection";

const { isConnected } = useConnection();
const { currentFile, stop, error } = useCast();

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
