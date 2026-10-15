<script setup lang="ts">
import { findNodeByPath } from "~~/app/features/library/logic/library";
import PlaybackScrubber from "~~/app/features/playback/components/PlaybackScrubber.vue";
import { clampPosition, describeTvState, isTvLost } from "~~/app/features/playback/logic/playback";
import { trickplayFrameAt, type TrickplayFrame } from "~~/app/features/playback/logic/trickplay";
import { injectPlayback } from "~~/app/features/playback/ports/playback";
import { useNowPlayingDetails } from "~~/app/features/playback/useNowPlayingDetails";
import { trickplaySpriteUrl } from "~~/app/lib/api";

const router = useRouter();
const playback = injectPlayback();
const { session, completed, loading, error, commandError, pending, sending } = playback;
const path = computed(() => session.value?.intent.relativePath);
const completedPath = computed(() => completed.value?.relativePath);
const preview = ref<TrickplayFrame | null>(null);

const { tree } = useLibrary();
// Only video has frames; a file the library hasn't shown yet waits until it does.
const previewable = computed(() => findNodeByPath(tree.value, path.value ?? "")?.kind === "video");
const { nextEpisode, subtitles, subtitlesStatus, trickplay, trickplayError } = useNowPlayingDetails(
  path,
  completedPath,
  previewable,
);

const observation = computed(() => session.value?.observation ?? null);
const actualState = computed(() => observation.value?.state);
const position = computed(() => observation.value?.positionSeconds ?? 0);
const duration = computed(() => observation.value?.durationSeconds ?? 0);
const requestedPosition = computed(
  () => session.value?.intent.pendingSeek?.positionSeconds ?? position.value,
);
const wantsPlaying = computed(() => session.value?.intent.desiredState === "playing");
const desiredState = computed(() => session.value?.intent.desiredState ?? "paused");
const spriteUrl = computed(() =>
  path.value && preview.value ? trickplaySpriteUrl(path.value, preview.value.sheet) : null,
);

const showPreview = (target: number): void => {
  if (trickplay.value) {
    preview.value = trickplayFrameAt(target, duration.value, trickplay.value);
  }
};

const seek = (target: number): void => {
  preview.value = null;
  void playback.updateIntent(desiredState.value, { seekPositionSeconds: target });
};

const skip = (seconds: number): void =>
  seek(clampPosition(requestedPosition.value + seconds, duration.value));

const OFF = "off";
const subtitleTrack = computed({
  get: () => observation.value?.subtitleIndex ?? OFF,
  set: (track: number | typeof OFF) =>
    void playback.updateIntent(desiredState.value, {
      subtitleIndex: track === OFF ? null : track,
    }),
});
const subtitleChoices = computed(() => [
  {
    value: OFF,
    label:
      subtitlesStatus.value === "error"
        ? "Could not load subtitles"
        : subtitlesStatus.value !== "success"
          ? "Loading subtitles…"
          : subtitles.value.length === 0
            ? "No subtitles available"
            : "Off",
  },
  ...subtitles.value.map((track) => ({
    value: track.index,
    label: track.language ? `${track.label} (${track.language})` : track.label,
  })),
]);
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center gap-3">
      <UButton
        variant="ghost"
        color="neutral"
        size="sm"
        icon="i-lucide-arrow-left"
        aria-label="Go back"
        @click="router.back()"
      />
      <h1 class="page-title truncate">Now Playing</h1>
    </div>

    <div v-if="loading && !session" class="space-y-3" aria-live="polite">
      <USkeleton class="h-56 w-full rounded" />
      <USkeleton class="h-5 w-2/3" />
    </div>

    <div v-else-if="!session && completed" class="space-y-4">
      <h2>Finished: {{ completed.title }}</h2>
      <UButton
        v-if="nextEpisode?.relativePath"
        :loading="sending"
        @click="void playback.start(nextEpisode.relativePath)"
        >Play next: {{ nextEpisode.title }}</UButton
      >
      <UAlert v-if="commandError" color="error" :title="commandError" />
    </div>

    <UAlert
      v-else-if="!session && !error"
      color="neutral"
      variant="soft"
      title="Nothing is playing"
      description="Choose a file from Library, Search, or Favourites."
    />

    <template v-else-if="session">
      <div
        class="relative flex aspect-video items-center justify-center overflow-hidden rounded-lg border border-neutral-800 bg-neutral-900"
      >
        <UIcon name="i-lucide-film" class="size-16 text-neutral-700" />
        <div class="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/90 px-4 pb-4 pt-10">
          <h2 class="truncate text-lg font-semibold">{{ session.intent.title }}</h2>
          <p
            aria-live="polite"
            class="text-sm"
            :class="isTvLost(session) ? 'text-amber-400' : 'text-neutral-300'"
          >
            {{ describeTvState(session, pending) }}
          </p>
        </div>
      </div>

      <p
        v-if="session.tvConnected && actualState === 'error' && observation?.errorMessage"
        role="alert"
        class="text-center text-sm text-red-400"
      >
        {{ observation.errorMessage }}
      </p>

      <UButton
        v-if="actualState === 'ended' && nextEpisode?.relativePath"
        :loading="sending"
        @click="void playback.start(nextEpisode.relativePath)"
      >
        Play next: {{ nextEpisode.title }}
      </UButton>

      <PlaybackScrubber
        :position="requestedPosition"
        :duration="duration"
        :preview="preview"
        :sprite-url="spriteUrl"
        :thumb-width="trickplay?.thumbWidth ?? 160"
        :thumb-height="trickplay?.thumbHeight ?? 90"
        :disabled="!session.tvConnected || sending"
        @preview="showPreview"
        @commit="seek"
        @cancel="preview = null"
      />

      <div class="flex items-center justify-center gap-4">
        <UButton
          aria-label="Back 10 seconds"
          :disabled="!session.tvConnected || sending || duration <= 0"
          @click="skip(-10)"
          >−10s</UButton
        >
        <UButton
          size="xl"
          color="neutral"
          variant="soft"
          icon="i-lucide-square"
          aria-label="Stop playback"
          :disabled="sending"
          @click="void playback.updateIntent('stopped')"
        />
        <UButton
          size="xl"
          class="rounded-full"
          :icon="wantsPlaying ? 'i-lucide-pause' : 'i-lucide-play'"
          :aria-label="wantsPlaying ? 'Pause playback' : 'Resume playback'"
          :loading="sending"
          @click="void playback.updateIntent(wantsPlaying ? 'paused' : 'playing')"
        />
        <UButton
          aria-label="Forward 10 seconds"
          :disabled="!session.tvConnected || sending || duration <= 0"
          @click="skip(10)"
          >+10s</UButton
        >
      </div>

      <UFormField label="Subtitles">
        <USelect
          v-model="subtitleTrack"
          :items="subtitleChoices"
          :disabled="sending || subtitlesStatus !== 'success' || subtitles.length === 0"
          class="w-full"
        />
      </UFormField>

      <UAlert
        v-if="commandError"
        color="error"
        variant="soft"
        title="Control not sent"
        :description="commandError"
      />
      <p v-if="trickplayError" class="text-sm text-amber-400">
        Preview images are unavailable. Seeking still works.
      </p>
    </template>
  </div>
</template>
