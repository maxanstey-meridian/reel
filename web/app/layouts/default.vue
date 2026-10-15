<script setup lang="ts">
import MiniPlayer from "~~/app/features/playback/components/MiniPlayer.vue";
import { providePlayback } from "~~/app/features/playback/ports/playback";
import { usePlaybackSession } from "~~/app/features/playback/usePlaybackSession";
import { ServerUnreachableError } from "~~/app/lib/api";
import { provideFavourites } from "~~/app/ports/favourites";
import { provideTags } from "~~/app/ports/tags";

const { session, pending, error } = providePlayback(usePlaybackSession());
provideFavourites(useFavourites());
provideTags(useTags());
// A lost pairing never shows here: the API sends the phone back to the connect page instead.
const connectionProblem = computed(() => {
  if (!error.value) {
    return null;
  }
  if (error.value instanceof ServerUnreachableError) {
    return {
      title: "Can't reach Reel — retrying",
      description: "Check the Studio is awake and this phone is on the home Wi-Fi.",
    };
  }
  return { title: "Reel had a problem — retrying", description: error.value.message };
});
const route = useRoute();
const navItems = [
  { path: "/continue-watching", label: "Continue", icon: "i-lucide-play-circle" },
  { path: "/library", label: "Library", icon: "i-lucide-film" },
  { path: "/search", label: "Search", icon: "i-lucide-search" },
  { path: "/favourites", label: "Favourites", icon: "i-lucide-heart" },
  { path: "/settings", label: "Settings", icon: "i-lucide-settings" },
];
const isActive = (path: string) => route.path.startsWith(path);
</script>

<template>
  <div
    class="flex flex-1 flex-col bg-neutral-950 text-white pb-[calc(65px+env(safe-area-inset-bottom))]"
  >
    <main class="mx-auto w-full max-w-md flex-1 p-4">
      <UAlert
        v-if="connectionProblem"
        class="mb-4"
        color="warning"
        variant="soft"
        icon="i-lucide-wifi-off"
        :title="connectionProblem.title"
        :description="connectionProblem.description"
      />
      <slot />
    </main>

    <MiniPlayer
      v-if="session"
      class="sticky bottom-[calc(65px+env(safe-area-inset-bottom))]"
      :session="session"
      :pending="pending"
      @open="navigateTo('/now-playing')"
    />

    <nav
      class="fixed inset-x-0 bottom-0 z-50 border-t border-neutral-800 bg-neutral-950 flex pb-[env(safe-area-inset-bottom)]"
      data-testid="bottom-nav"
    >
      <NuxtLink
        v-for="item in navItems"
        :key="item.path"
        :to="item.path"
        class="flex-1 flex flex-col items-center gap-1 py-3 transition-colors"
        :class="isActive(item.path) ? 'text-green-500' : 'text-neutral-500'"
        :data-active="isActive(item.path) || undefined"
      >
        <UIcon :name="item.icon" class="size-5" />
        <span class="text-xs">{{ item.label }}</span>
      </NuxtLink>
    </nav>
  </div>
</template>
