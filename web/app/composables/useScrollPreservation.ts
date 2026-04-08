import type { Ref } from "vue";

export const useScrollPreservation = (
  loading: Ref<boolean>,
  currentPath: Ref<string[]>,
) => {
  const scrollTop = useState<number>("library:scrollTop", () => 0);

  let scrollContainer: HTMLElement | null = null;
  let debounceTimer: ReturnType<typeof setTimeout> | null = null;

  const handleScroll = () => {
    if (debounceTimer) {
      clearTimeout(debounceTimer);
    }
    debounceTimer = setTimeout(() => {
      if (scrollContainer) {
        scrollTop.value = scrollContainer.scrollTop;
      }
    }, 150);
  };

  onMounted(() => {
    scrollContainer = document.querySelector("main");
    if (!scrollContainer) {
      return;
    }

    scrollContainer.addEventListener("scroll", handleScroll, { passive: true });
  });

  // Restore scroll position once loading completes on mount
  watch(
    loading,
    (val) => {
      if (!val && scrollTop.value > 0 && scrollContainer) {
        nextTick(() => {
          scrollContainer!.scrollTop = scrollTop.value;
        });
      }
    },
    { once: true },
  );

  // Reset scroll position when navigating into a subfolder
  watch(currentPath, () => {
    scrollTop.value = 0;
    if (scrollContainer) {
      scrollContainer.scrollTo(0, 0);
    }
  });

  onUnmounted(() => {
    if (scrollContainer) {
      scrollContainer.removeEventListener("scroll", handleScroll);
    }
    if (debounceTimer) {
      clearTimeout(debounceTimer);
    }
  });
};
