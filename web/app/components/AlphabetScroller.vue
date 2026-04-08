<script setup lang="ts">
import { ref, computed } from "vue";
import { ALPHABET } from "~~/app/utils/alphabetScroller";

const props = defineProps<{
  names: string[];
}>();

const emit = defineEmits<{ scrollTo: [letter: string] }>();

const visible = computed(() => props.names.length > 10);
const activeLetter = ref<string | null>(null);

const handleTap = (letter: string) => {
  activeLetter.value = letter;
  emit("scrollTo", letter);
};

const handleTouchMove = (e: TouchEvent) => {
  const touch = e.touches[0];
  if (!touch) {
    return;
  }
  const el = document.elementFromPoint(touch.clientX, touch.clientY);
  if (el instanceof HTMLElement && el.dataset.letter) {
    const letter = el.dataset.letter;
    if (letter !== activeLetter.value) {
      activeLetter.value = letter;
      emit("scrollTo", letter);
    }
  }
};

const handleTouchEnd = () => {
  activeLetter.value = null;
};
</script>

<template>
  <div
    v-if="visible"
    class="fixed right-2 top-1/2 -translate-y-1/2 bg-neutral-900/80 rounded-lg py-1 px-0.5 z-20 flex flex-col items-center select-none touch-none"
    @touchmove.prevent="handleTouchMove"
    @touchend="handleTouchEnd"
  >
    <button
      v-for="letter in ALPHABET"
      :key="letter"
      :data-letter="letter"
      class="text-[10px] leading-[18px] w-5 text-center transition-colors"
      :class="activeLetter === letter ? 'text-white' : 'text-neutral-500 hover:text-white'"
      @click="handleTap(letter)"
    >
      {{ letter }}
    </button>
  </div>
</template>
