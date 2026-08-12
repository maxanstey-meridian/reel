<script setup lang="ts">
import { clampPosition, formatPlaybackTime } from "../logic/playback";
import type { TrickplayFrame } from "../logic/trickplay";
import { createScrubInteraction } from "../logic/trickplay";
import TrickplayPreview from "./TrickplayPreview.vue";

const props = defineProps<{
  position: number;
  duration: number;
  preview: TrickplayFrame | null;
  spriteUrl: string | null;
  thumbWidth: number;
  thumbHeight: number;
  disabled?: boolean;
}>();
const emit = defineEmits<{ preview: [position: number]; commit: [position: number]; cancel: [] }>();

const scrub = createScrubInteraction(
  () => props.duration,
  (position) => emit("preview", position),
  (position) => emit("commit", position),
);
const dragging = ref(false);
const localPosition = ref(0);
const displayedPosition = computed(() => (dragging.value ? localPosition.value : props.position));

const readPosition = (event: Event): number =>
  clampPosition(Number((event.target as HTMLInputElement).value), props.duration);

const onInput = (event: Event): void => {
  scrub.move(readPosition(event));
  dragging.value = true;
  localPosition.value = scrub.position;
};

const onChange = (event: Event): void => {
  scrub.release(readPosition(event));
  dragging.value = false;
};

const cancel = (): void => {
  if (!scrub.dragging) {
    return;
  }
  scrub.cancel();
  dragging.value = false;
  emit("cancel");
};
</script>

<template>
  <div class="relative pt-2">
    <TrickplayPreview
      v-if="dragging && preview"
      :frame="preview"
      :sprite-url="spriteUrl"
      :width="thumbWidth"
      :height="thumbHeight"
    />
    <label for="playback-position" class="sr-only">Playback position</label>
    <input
      id="playback-position"
      type="range"
      min="0"
      :max="Math.max(duration, 0)"
      step="0.1"
      :value="displayedPosition"
      :disabled="disabled || duration <= 0"
      class="w-full accent-green-500 disabled:opacity-50"
      @input="onInput"
      @change="onChange"
      @keyup.escape="cancel"
      @blur="cancel"
    />
    <div class="flex justify-between text-xs tabular-nums text-neutral-500">
      <span>{{ formatPlaybackTime(displayedPosition) }}</span>
      <span>{{ formatPlaybackTime(duration) }}</span>
    </div>
  </div>
</template>
