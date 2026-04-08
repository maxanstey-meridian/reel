import { ref } from "vue";

const PULL_THRESHOLD = 60;
const MAX_PULL = 100;

export const usePullToRefresh = (onRefresh: () => Promise<void>) => {
  const refreshing = ref(false);
  const pullOffset = ref(0);

  let scrollContainer: HTMLElement | null = null;
  let startY = 0;
  let pulling = false;

  const handleTouchStart = (e: TouchEvent) => {
    if (refreshing.value || !scrollContainer || scrollContainer.scrollTop > 0) {
      return;
    }
    const touch = e.touches[0];
    if (!touch) return;
    startY = touch.clientY;
    pulling = true;
  };

  const handleTouchMove = (e: TouchEvent) => {
    if (!pulling || refreshing.value || !scrollContainer) {
      return;
    }

    if (scrollContainer.scrollTop > 0) {
      pulling = false;
      pullOffset.value = 0;
      return;
    }

    const touch = e.touches[0];
    if (!touch) return;
    const deltaY = touch.clientY - startY;

    if (deltaY > 0) {
      e.preventDefault();
      pullOffset.value = Math.min(deltaY, MAX_PULL);
    } else {
      pullOffset.value = 0;
    }
  };

  const handleTouchEnd = async () => {
    if (!pulling) {
      return;
    }
    pulling = false;

    if (pullOffset.value >= PULL_THRESHOLD) {
      refreshing.value = true;
      pullOffset.value = 0;
      try {
        await onRefresh();
      } catch {
        // Refresh errors are handled by the caller's own error state
      } finally {
        refreshing.value = false;
      }
    } else {
      pullOffset.value = 0;
    }
  };

  onMounted(() => {
    scrollContainer = document.querySelector("main");
    if (!scrollContainer) {
      return;
    }

    scrollContainer.addEventListener("touchstart", handleTouchStart, { passive: true });
    scrollContainer.addEventListener("touchmove", handleTouchMove, { passive: false });
    scrollContainer.addEventListener("touchend", handleTouchEnd);
  });

  onUnmounted(() => {
    if (scrollContainer) {
      scrollContainer.removeEventListener("touchstart", handleTouchStart);
      scrollContainer.removeEventListener("touchmove", handleTouchMove);
      scrollContainer.removeEventListener("touchend", handleTouchEnd);
    }
  });

  return { refreshing, pullOffset };
};
