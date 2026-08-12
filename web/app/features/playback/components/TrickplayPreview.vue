<script setup lang="ts">
import { formatPlaybackTime } from "../logic/playback";
import type { TrickplayFrame } from "../logic/trickplay";

defineProps<{
  frame: TrickplayFrame;
  spriteUrl: string | null;
  width: number;
  height: number;
}>();
</script>

<template>
  <div
    class="pointer-events-none absolute bottom-8 left-1/2 z-10 -translate-x-1/2 overflow-hidden rounded border border-neutral-700 bg-neutral-950 shadow-xl"
  >
    <div
      :style="{ width: `${width}px`, height: `${height}px` }"
      class="max-w-[70vw] bg-neutral-900"
    >
      <div
        v-if="spriteUrl"
        class="h-full w-full"
        :style="{
          backgroundImage: `url(${spriteUrl})`,
          backgroundPosition: `-${frame.x}px -${frame.y}px`,
        }"
      />
    </div>
    <p class="px-2 py-1 text-center text-xs font-medium">
      {{ formatPlaybackTime(frame.positionSeconds) }}
    </p>
  </div>
</template>
