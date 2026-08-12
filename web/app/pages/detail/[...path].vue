<script setup lang="ts">
import { useLibrary } from "../../composables/useLibrary";
import { useProgress } from "../../composables/useProgress";
import { formatPlaybackTime, hasIncompleteProgress } from "../../features/playback/logic/playback";
import { injectPlayback } from "../../features/playback/playback-context";
import { findNodeByPath } from "../../utils/findNodeByPath";

const route = useRoute();
const router = useRouter();
const { tree, loading: libraryLoading } = useLibrary();
const { entries, loading: progressLoading, fetch: fetchProgress } = useProgress();
const playback = injectPlayback();
const { pending: playbackPending, commandError: playbackCommandError } = playback;
const choosingStart = ref(false);

const relativePath = computed(() => {
  const path = route.params.path;
  return Array.isArray(path) ? path.join("/") : (path ?? "");
});

const fileName = computed(() => relativePath.value.split("/").pop() ?? "");
const node = computed(() => findNodeByPath(tree.value, relativePath.value));
const progress = computed(() =>
  entries.value.find((entry) => entry.relativePath === relativePath.value),
);
const displayTitle = computed(() => node.value?.parsedTitle ?? fileName.value);
const loading = computed(() => libraryLoading.value || progressLoading.value);
const canResume = computed(() => hasIncompleteProgress(progress.value));

const requestPlay = async (position: number): Promise<void> => {
  if (playbackPending.value) {
    return;
  }
  try {
    await playback.start(relativePath.value, position);
    choosingStart.value = false;
    await router.push("/now-playing");
  } catch {
    // The layout-owned playback state exposes the actionable API error.
  }
};

const play = (): void => {
  if (canResume.value) {
    choosingStart.value = true;
  } else {
    void requestPlay(0);
  }
};

const formatDuration = (seconds: number): string => {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
};

const formattedDuration = computed(() => {
  const duration = progress.value?.durationSeconds;
  return duration && duration > 0 ? formatDuration(duration) : null;
});

const progressPercent = computed(() => {
  const position = progress.value?.positionSeconds;
  const duration = progress.value?.durationSeconds;
  return position && position > 0 && duration && duration > 0 ? (position / duration) * 100 : null;
});

const remainingTime = computed(() => {
  const position = progress.value?.positionSeconds;
  const duration = progress.value?.durationSeconds;
  if (!position || !duration || position <= 0 || duration <= position) {
    return null;
  }
  return `${formatDuration(duration - position)} remaining`;
});

onMounted(fetchProgress);
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center gap-3">
      <UButton
        variant="ghost"
        color="neutral"
        size="sm"
        icon="i-lucide-arrow-left"
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
        <UIcon name="i-lucide-film" class="size-16" />
      </div>

      <div v-if="progressPercent != null" class="space-y-1">
        <div class="h-1 bg-neutral-700 rounded-full overflow-hidden">
          <div
            class="h-full bg-green-500"
            :style="{ width: `${Math.min(progressPercent, 100)}%` }"
          />
        </div>
        <p v-if="remainingTime" class="text-xs text-neutral-500">{{ remainingTime }}</p>
      </div>

      <div class="flex items-center gap-2">
        <UBadge v-if="fileName.split('.').pop()" size="xs" color="neutral" variant="subtle">
          .{{ fileName.split(".").pop() }}
        </UBadge>
        <UBadge v-if="formattedDuration" size="xs" color="neutral" variant="subtle">
          {{ formattedDuration }}
        </UBadge>
        <span class="text-xs text-neutral-500 truncate">{{ relativePath }}</span>
      </div>

      <div class="space-y-2 pt-2">
        <template v-if="choosingStart && progress">
          <p class="text-sm font-medium">Continue watching?</p>
          <UButton
            block
            icon="i-lucide-play"
            :loading="playbackPending"
            :disabled="playbackPending"
            @click="requestPlay(progress.positionSeconds)"
          >
            Resume at {{ formatPlaybackTime(progress.positionSeconds) }}
          </UButton>
          <UButton
            block
            color="neutral"
            variant="soft"
            :loading="playbackPending"
            :disabled="playbackPending"
            @click="requestPlay(0)"
            >Start Over</UButton
          >
        </template>
        <UButton v-else block icon="i-lucide-play" :loading="playbackPending" @click="play"
          >Play</UButton
        >
        <UAlert
          v-if="playbackCommandError"
          color="error"
          variant="soft"
          :description="playbackCommandError"
        />
      </div>
    </template>
  </div>
</template>
