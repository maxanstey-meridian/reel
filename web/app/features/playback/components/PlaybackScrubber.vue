<script setup lang="ts">
import { clampPosition, formatPlaybackTime } from "../logic/playback";
import type { TrickplayFrame } from "../logic/trickplay";
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

// Set while the thumb is being dragged; only release sends a seek.
const dragPosition = ref<number | null>(null);
const displayedPosition = computed(() => dragPosition.value ?? props.position);

const readPosition = (event: Event): number =>
  clampPosition(Number((event.target as HTMLInputElement).value), props.duration);

const onInput = (event: Event): void => {
  dragPosition.value = readPosition(event);
  emit("preview", dragPosition.value);
};

const onChange = (event: Event): void => {
  if (dragPosition.value !== null) {
    dragPosition.value = null;
    emit("commit", readPosition(event));
  }
};

const cancel = (): void => {
  if (dragPosition.value !== null) {
    dragPosition.value = null;
    emit("cancel");
  }
};
</script>

<template>
  <div class="relative pt-2">
    <TrickplayPreview
      v-if="dragPosition !== null && preview"
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
      @pointercancel="cancel"
      @touchcancel="cancel"
      @keyup.escape="cancel"
      @blur="cancel"
    />
    <div class="flex justify-between text-xs tabular-nums text-neutral-500">
      <span>{{ formatPlaybackTime(displayedPosition) }}</span>
      <span>{{ formatPlaybackTime(Math.max(0, duration - displayedPosition)) }} remaining</span>
    </div>
  </div>
</template>
