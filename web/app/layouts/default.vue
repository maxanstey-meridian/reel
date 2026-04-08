<script setup lang="ts">
const route = useRoute();

const navItems = [
  { path: "/library", label: "Library", icon: "i-lucide-film" },
  { path: "/search", label: "Search", icon: "i-lucide-search" },
  { path: "/favourites", label: "Favourites", icon: "i-lucide-heart" },
  { path: "/now-playing", label: "Now Playing", icon: "i-lucide-cast" },
];

const isActive = (path: string) => route.path.startsWith(path);
</script>

<template>
  <div class="h-dvh flex flex-col bg-neutral-950 text-white">
    <main class="flex-1 overflow-y-auto">
      <div class="mx-auto w-full max-w-md p-4">
        <slot />
      </div>
    </main>

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
