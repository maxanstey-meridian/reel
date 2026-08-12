<script setup lang="ts">
import PlaybackScrubber from "~~/app/features/playback/components/PlaybackScrubber.vue";
import { playbackTitle } from "~~/app/features/playback/logic/playback";
import { trickplayFrameAt, type TrickplayFrame } from "~~/app/features/playback/logic/trickplay";
import { injectPlayback } from "~~/app/features/playback/playback-context";
import { api, type SubtitleTrack, type TrickplayMetadata } from "~~/app/lib/api";

const router = useRouter();
const { serverUrl, secretKey } = useConnection();
const playback = injectPlayback();
const { session, loading, error, commandError, pending } = playback;
const subtitles = ref<SubtitleTrack[]>([]);
const metadata = ref<TrickplayMetadata | null>(null);
const preview = ref<TrickplayFrame | null>(null);
const spriteUrl = ref<string | null>(null);
const mediaError = ref<string | null>(null);
let loadedSheet: number | null = null;
let spriteRequest = 0;
let mediaGeneration = 0;
let mediaController: AbortController | null = null;
let spriteController: AbortController | null = null;

const observation = computed(() => session.value?.observation ?? null);
const actualState = computed(() => observation.value?.state ?? "loading");
const title = computed(() => playbackTitle(session.value?.intent.relativePath ?? ""));
const position = computed(() => observation.value?.positionSeconds ?? 0);
const duration = computed(() => observation.value?.durationSeconds ?? 0);
const isActuallyPlaying = computed(() => actualState.value === "playing");

const revokeSprite = (): void => {
  if (spriteUrl.value) {
    URL.revokeObjectURL(spriteUrl.value);
  }
  spriteUrl.value = null;
  loadedSheet = null;
};

const loadMediaControls = async (path: string): Promise<void> => {
  const generation = ++mediaGeneration;
  mediaController?.abort();
  mediaController = new AbortController();
  spriteRequest++;
  spriteController?.abort();
  revokeSprite();
  metadata.value = null;
  subtitles.value = [];
  mediaError.value = null;
  try {
    const [tracks, trickplay] = await Promise.all([
      api.subtitlesList(path, mediaController.signal),
      api.trickplayInfo(path, mediaController.signal),
    ]);
    if (generation !== mediaGeneration) {
      return;
    }
    subtitles.value = tracks;
    metadata.value = trickplay;
  } catch (caught) {
    if (generation !== mediaGeneration) {
      return;
    }
    mediaError.value = caught instanceof Error ? caught.message : "Preview controls unavailable";
  }
};

watch(
  () => session.value?.intent.relativePath,
  (path) => {
    if (path) {
      void loadMediaControls(path);
    } else {
      mediaGeneration++;
      mediaController?.abort();
      spriteController?.abort();
      revokeSprite();
    }
  },
  { immediate: true },
);

const showPreview = async (target: number): Promise<void> => {
  const currentSession = session.value;
  const info = metadata.value;
  if (!currentSession || !info) {
    return;
  }
  const frame = trickplayFrameAt(target, duration.value, info);
  if (!frame) {
    preview.value = null;
    mediaError.value = "Trickplay preview unavailable";
    return;
  }
  preview.value = frame;
  if (loadedSheet === frame.sheet) {
    return;
  }
  const request = ++spriteRequest;
  spriteController?.abort();
  spriteController = new AbortController();
  const path = currentSession.intent.relativePath;
  try {
    const blob = await api.trickplaySprite(
      serverUrl.value,
      secretKey.value,
      path,
      frame.sheet,
      spriteController.signal,
    );
    if (request !== spriteRequest || session.value?.intent.relativePath !== path) {
      return;
    }
    revokeSprite();
    spriteUrl.value = URL.createObjectURL(blob);
    loadedSheet = frame.sheet;
  } catch (caught) {
    if (request !== spriteRequest) {
      return;
    }
    mediaError.value = caught instanceof Error ? caught.message : "Trickplay preview unavailable";
  }
};

const sendIntent = async (
  desiredState: "playing" | "paused" | "stopped",
  options: { subtitleIndex?: number | null; seekPositionSeconds?: number } = {},
): Promise<void> => {
  try {
    await playback.updateIntent(desiredState, options);
  } catch {
    // The layout-owned context retains and renders the request error.
  }
};

const togglePlayPause = (): void => {
  void sendIntent(isActuallyPlaying.value ? "paused" : "playing");
};

const seek = (target: number): void => {
  preview.value = null;
  void sendIntent(session.value?.intent.desiredState ?? "paused", { seekPositionSeconds: target });
};

const selectSubtitle = (event: Event): void => {
  const value = Number((event.target as HTMLSelectElement).value);
  void sendIntent(session.value?.intent.desiredState ?? "paused", {
    subtitleIndex: value === -1 ? null : value,
  });
};

onBeforeUnmount(() => {
  mediaGeneration++;
  mediaController?.abort();
  spriteController?.abort();
  spriteRequest++;
  revokeSprite();
});
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

    <UAlert
      v-else-if="!session"
      color="neutral"
      variant="soft"
      title="Nothing is playing"
      description="Choose a file from Library, Search, or Favourites."
    />

    <template v-else>
      <div
        class="relative flex aspect-video items-center justify-center overflow-hidden rounded-lg border border-neutral-800 bg-neutral-900"
      >
        <UIcon name="i-lucide-film" class="size-16 text-neutral-700" />
        <div class="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/90 px-4 pb-4 pt-10">
          <h2 class="truncate text-lg font-semibold">{{ title }}</h2>
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

      <PlaybackScrubber
        :position="position"
        :duration="duration"
        :preview="preview"
        :sprite-url="spriteUrl"
        :thumb-width="metadata?.thumbWidth ?? 160"
        :thumb-height="metadata?.thumbHeight ?? 90"
        :disabled="!session.tvConnected || pending"
        @preview="showPreview"
        @commit="seek"
        @cancel="preview = null"
      />

      <div class="flex items-center justify-center gap-4">
        <UButton
          size="xl"
          color="neutral"
          variant="soft"
          icon="i-lucide-square"
          aria-label="Stop playback"
          :disabled="pending"
          @click="sendIntent('stopped')"
        />
        <UButton
          size="xl"
          class="rounded-full"
          :icon="isActuallyPlaying ? 'i-lucide-pause' : 'i-lucide-play'"
          :aria-label="isActuallyPlaying ? 'Pause playback' : 'Play playback'"
          :loading="pending"
          @click="togglePlayPause"
        />
      </div>

      <div>
        <label for="subtitle-track" class="mb-2 block text-sm font-medium">Subtitles</label>
        <select
          id="subtitle-track"
          class="w-full rounded border border-neutral-700 bg-neutral-900 px-3 py-2 text-sm focus:border-green-500 focus:outline-none"
          :value="observation?.subtitleIndex ?? -1"
          :disabled="pending"
          @change="selectSubtitle"
        >
          <option :value="-1">Off</option>
          <option v-for="track in subtitles" :key="track.index" :value="track.index">
            {{ track.label }}{{ track.language ? ` (${track.language})` : "" }}
          </option>
        </select>
      </div>

      <UAlert
        v-if="error"
        color="error"
        variant="soft"
        title="Connection problem"
        :description="error"
      />
      <UAlert
        v-if="commandError"
        color="error"
        variant="soft"
        title="Control not sent"
        :description="commandError"
      />
      <p v-if="mediaError" class="text-sm text-amber-400">{{ mediaError }}</p>
    </template>
  </div>
</template>
