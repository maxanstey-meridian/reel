<script setup lang="ts">
import MiniPlayer from "~~/app/features/playback/components/MiniPlayer.vue";
import { providePlayback } from "~~/app/features/playback/playback-context";
import { usePlaybackSession } from "~~/app/features/playback/usePlaybackSession";
import { navItems } from "~~/app/utils/navItems";

const route = useRoute();
const router = useRouter();
const { isConnected } = useConnection();
if (!isConnected.value) {
  await navigateTo("/connect", { replace: true });
}
const playback = isConnected.value ? providePlayback(usePlaybackSession()) : null;
const session = computed(() => playback?.session.value ?? null);
const pending = computed(() => playback?.pending.value ?? false);

const isActive = (path: string) => route.path.startsWith(path);
</script>

<template>
  <div v-if="playback" class="flex min-h-0 flex-1 flex-col bg-neutral-950 text-white">
    <header class="shrink-0">
      <div class="mx-auto w-full max-w-md flex justify-end px-4 pt-3">
        <NuxtLink
          to="/settings"
          class="transition-colors"
          :class="route.path === '/settings' ? 'text-white' : 'text-neutral-500 hover:text-white'"
          data-testid="settings-link"
          aria-label="Settings"
        >
          <UIcon name="i-lucide-settings" class="size-5" />
        </NuxtLink>
      </div>
    </header>

    <main class="flex-1 overflow-y-auto">
      <div class="mx-auto w-full max-w-md p-4">
        <slot />
      </div>
    </main>

    <MiniPlayer
      v-if="session"
      :session="session"
      :pending="pending"
      @open="router.push('/now-playing')"
    />

    <nav
      class="shrink-0 border-t border-neutral-800 flex pb-[env(safe-area-inset-bottom)]"
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
