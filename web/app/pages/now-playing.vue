<script setup lang="ts">
import PlaybackScrubber from "~~/app/features/playback/components/PlaybackScrubber.vue";
import { clampPosition } from "~~/app/features/playback/logic/playback";
import { trickplayFrameAt, type TrickplayFrame } from "~~/app/features/playback/logic/trickplay";
import { injectPlayback } from "~~/app/features/playback/ports/playback";
import { client, trickplaySpriteUrl, unwrap } from "~~/app/lib/api";

const router = useRouter();
const playback = injectPlayback();
const { session, completed, loading, error, commandError, pending, sending } = playback;
const path = computed(() => session.value?.intent.relativePath);
const completedPath = computed(() => completed.value?.relativePath);
const preview = ref<TrickplayFrame | null>(null);

const { data: nextEpisode } = useAsyncData(
  () => `next-episode:${completedPath.value}`,
  async (_nuxtApp, { signal }) =>
    completedPath.value
      ? unwrap(
          await client.GET("/api/library/next", {
            params: { query: { path: completedPath.value } },
            signal,
          }),
        )
      : null,
);
const { data: subtitles, status: subtitlesStatus } = useAsyncData(
  () => `subtitles:${path.value}`,
  async (_nuxtApp, { signal }) =>
    path.value
      ? unwrap(
          await client.GET("/api/files/subtitles", {
            params: { query: { path: path.value } },
            signal,
          }),
        )
      : [],
  { default: () => [] },
);
const { data: metadata, error: metadataError } = useAsyncData(
  () => `trickplay:${path.value}`,
  async (_nuxtApp, { signal }) =>
    path.value
      ? unwrap(
          await client.GET("/api/files/trickplay", {
            params: { query: { path: path.value } },
            signal,
          }),
        )
      : null,
);

const observation = computed(() => session.value?.observation ?? null);
const actualState = computed(() => observation.value?.state ?? "loading");
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
  if (metadata.value) {
    preview.value = trickplayFrameAt(target, duration.value, metadata.value);
  }
};

const seek = (target: number): void => {
  preview.value = null;
  void playback.updateIntent(desiredState.value, { seekPositionSeconds: target });
};

const skip = (seconds: number): void =>
  seek(clampPosition(requestedPosition.value + seconds, duration.value));

const selectSubtitle = (event: Event): void => {
  const value = Number((event.target as HTMLSelectElement).value);
  void playback.updateIntent(desiredState.value, { subtitleIndex: value === -1 ? null : value });
};
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
            class="text-sm capitalize"
            :class="session.tvConnected ? 'text-neutral-300' : 'text-amber-400'"
          >
            {{ session.tvConnected ? actualState : "TV disconnected" }}
          </p>
        </div>
      </div>

      <div aria-live="polite" class="min-h-5 text-center text-sm text-neutral-400">
        <span v-if="pending">Waiting for TV to apply control…</span>
        <span v-else-if="actualState === 'buffering'">Buffering on TV…</span>
        <span v-else-if="actualState === 'loading'">Loading on TV…</span>
        <span v-else-if="actualState === 'ended'">Playback ended</span>
        <span v-else-if="actualState === 'error'" class="text-red-400">
          {{ observation?.errorMessage || observation?.errorCode || "Playback failed on TV" }}
        </span>
      </div>

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
        :thumb-width="metadata?.thumbWidth ?? 160"
        :thumb-height="metadata?.thumbHeight ?? 90"
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
          :aria-label="wantsPlaying ? 'Pause playback' : 'Play playback'"
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

      <div>
        <label for="subtitle-track" class="mb-2 block text-sm font-medium">Subtitles</label>
        <select
          id="subtitle-track"
          class="w-full rounded border border-neutral-700 bg-neutral-900 px-3 py-2 text-sm focus:border-green-500 focus:outline-none"
          :value="observation?.subtitleIndex ?? -1"
          :disabled="sending || subtitlesStatus !== 'success' || subtitles.length === 0"
          @change="selectSubtitle"
        >
          <option :value="-1">
            {{
              subtitlesStatus === "error"
                ? "Could not load subtitles"
                : subtitlesStatus !== "success"
                  ? "Loading subtitles…"
                  : subtitles.length === 0
                    ? "No subtitles available"
                    : "Off"
            }}
          </option>
          <option v-for="track in subtitles" :key="track.index" :value="track.index">
            {{ track.label }}{{ track.language ? ` (${track.language})` : "" }}
          </option>
        </select>
      </div>

      <UAlert
        v-if="commandError"
        color="error"
        variant="soft"
        title="Control not sent"
        :description="commandError"
      />
      <p v-if="metadataError" class="text-sm text-amber-400">
        Preview images are unavailable. Seeking still works.
      </p>
    </template>
  </div>
</template>
