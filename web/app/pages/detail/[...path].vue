<script setup lang="ts">
import { findNodeByPath } from "~~/app/features/library/logic/library";
import {
  formatDuration,
  formatPlaybackTime,
  hasIncompleteProgress,
  progressFraction,
} from "~~/app/features/playback/logic/playback";
import { injectPlayback } from "~~/app/features/playback/ports/playback";

const route = useRoute();
const router = useRouter();
const library = useLibrary();
const { data: entries, status: progressStatus } = useProgress();
const playback = injectPlayback();
const { sending, commandError } = playback;
const choosingStart = ref(false);

const relativePath = computed(() => [route.params.path ?? []].flat().join("/"));
const node = computed(() => findNodeByPath(library.tree.value, relativePath.value));
const progress = computed(() =>
  entries.value.find((entry) => entry.relativePath === relativePath.value),
);
const displayTitle = computed(() => node.value?.parsedTitle ?? relativePath.value.split("/").pop());
const loading = computed(
  () => library.loading.value || (progressStatus.value === "pending" && entries.value.length === 0),
);
const percent = computed(() => progressFraction(progress.value) * 100);

const requestPlay = async (position: number): Promise<void> => {
  if (await playback.start(relativePath.value, position)) {
    choosingStart.value = false;
    await router.push("/now-playing");
  }
};

const play = (): void => {
  if (hasIncompleteProgress(progress.value)) {
    choosingStart.value = true;
  } else {
    void requestPlay(0);
  }
};
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center gap-3">
      <UButton
        variant="ghost"
        color="neutral"
        size="sm"
        icon="i-lucide-arrow-left"
        aria-label="Go back"
        @click="router.back()"
      />
      <h1 class="text-base font-semibold truncate flex-1">{{ displayTitle }}</h1>
    </div>

    <div v-if="loading" class="space-y-3">
      <USkeleton class="h-56 w-full rounded" />
      <USkeleton class="h-4 w-3/4" />
    </div>

    <template v-else>
      <div class="h-56 flex items-center justify-center rounded bg-neutral-800/50 text-neutral-500">
        <MediaThumbnail :node="node" />
      </div>

      <div v-if="progress && percent > 0" class="space-y-1">
        <div class="h-1 bg-neutral-700 rounded-full overflow-hidden">
          <div class="h-full bg-green-500" :style="{ width: `${percent}%` }" />
        </div>
        <p v-if="percent < 100" class="text-xs text-neutral-500">
          {{ formatDuration(progress.durationSeconds - progress.positionSeconds) }} remaining
        </p>
      </div>

      <div class="flex items-center gap-2">
        <UBadge v-if="node?.extension" size="xs" color="neutral" variant="subtle">
          {{ node.extension }}
        </UBadge>
        <UBadge v-if="progress?.durationSeconds" size="xs" color="neutral" variant="subtle">
          {{ formatDuration(progress.durationSeconds) }}
        </UBadge>
        <span class="text-xs text-neutral-500 truncate">{{ relativePath }}</span>
      </div>

      <div class="space-y-2 pt-2">
        <template v-if="choosingStart && progress">
          <p class="text-sm font-medium">Continue watching?</p>
          <UButton
            block
            icon="i-lucide-play"
            :loading="sending"
            @click="requestPlay(progress.positionSeconds)"
          >
            Resume at {{ formatPlaybackTime(progress.positionSeconds) }}
          </UButton>
          <UButton block color="neutral" variant="soft" :loading="sending" @click="requestPlay(0)"
            >Start Over</UButton
          >
        </template>
        <UButton v-else block icon="i-lucide-play" :loading="sending" @click="play"
          >Play on TV</UButton
        >
        <UAlert v-if="commandError" color="error" variant="soft" :description="commandError" />
      </div>

      <TagEditor :key="relativePath" :path="relativePath" type="file" />
    </template>
  </div>
</template>
