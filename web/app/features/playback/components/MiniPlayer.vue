<script setup lang="ts">
import type { PlaybackSession } from "~~/app/lib/api";
import { playbackTitle } from "../logic/playback";

const props = defineProps<{ session: PlaybackSession; pending: boolean }>();
defineEmits<{ open: [] }>();

const state = computed(() => props.session.observation?.state ?? "Waiting for TV");
</script>

<template>
  <button
    type="button"
    class="w-full flex items-center gap-3 border-t border-neutral-800 bg-neutral-900 px-4 py-3 text-left hover:bg-neutral-800 focus-visible:outline-2 focus-visible:outline-green-500"
    data-testid="mini-player"
    @click="$emit('open')"
  >
    <span
      class="flex size-9 shrink-0 items-center justify-center rounded bg-green-500/15 text-green-400"
    >
      <UIcon name="i-lucide-audio-lines" class="size-5" />
    </span>
    <span class="min-w-0 flex-1">
      <span class="block truncate text-sm font-medium">{{
        playbackTitle(session.intent.relativePath)
      }}</span>
      <span
        class="block text-xs capitalize"
        :class="session.tvConnected ? 'text-neutral-400' : 'text-amber-400'"
      >
        {{ session.tvConnected ? state : "TV disconnected" }}{{ pending ? " · Pending" : "" }}
      </span>
    </span>
    <UIcon name="i-lucide-chevron-up" class="size-5 text-neutral-500" />
  </button>
</template>
