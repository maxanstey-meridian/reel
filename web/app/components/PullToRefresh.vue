<script setup lang="ts">
import { useEventListener } from "@vueuse/core";

const props = defineProps<{ refresh: () => Promise<unknown> }>();
const THRESHOLD = 60;
const refreshing = ref(false);
const offset = ref(0);
let startY: number | null = null;

useEventListener(
  document,
  "touchstart",
  (event: TouchEvent) => {
    offset.value = 0;
    startY = refreshing.value || window.scrollY > 0 ? null : (event.touches[0]?.clientY ?? null);
  },
  { passive: true },
);

useEventListener(
  document,
  "touchmove",
  (event: TouchEvent) => {
    const y = event.touches[0]?.clientY;
    if (startY === null || y === undefined || window.scrollY > 0) {
      startY = null;
      offset.value = 0;
      return;
    }
    if (y > startY) {
      event.preventDefault();
    }
    offset.value = Math.min(Math.max(y - startY, 0), 100);
  },
  { passive: false },
);

// The system can take over a touch (a scroll, a gesture, a call); that is never a pull.
useEventListener(document, "touchcancel", () => {
  startY = null;
  offset.value = 0;
});

useEventListener(document, "touchend", async () => {
  const pulled = startY !== null && offset.value >= THRESHOLD;
  startY = null;
  offset.value = 0;
  if (pulled) {
    refreshing.value = true;
    await props.refresh().finally(() => {
      refreshing.value = false;
    });
  }
});
</script>

<template>
  <div
    v-if="refreshing || offset > 0"
    class="flex justify-center overflow-hidden transition-all"
    :style="{ height: refreshing ? '32px' : `${Math.min(offset, THRESHOLD)}px` }"
  >
    <UIcon
      :name="refreshing ? 'i-lucide-loader-2' : 'i-lucide-arrow-down'"
      class="size-5 text-neutral-500"
      :class="{ 'animate-spin': refreshing }"
    />
  </div>
</template>
