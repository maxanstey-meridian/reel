<script setup lang="ts">
definePageMeta({ layout: "blank" });

const { users, switchUser } = useUser();

const handleSelect = (id: number) => {
  switchUser(id);
  navigateTo("/library", { replace: true });
};

const AVATAR_COLORS = [
  "bg-green-700",
  "bg-blue-700",
  "bg-purple-700",
  "bg-amber-700",
  "bg-rose-700",
  "bg-cyan-700",
  "bg-orange-700",
  "bg-teal-700",
];

const colorForIndex = (index: number) => AVATAR_COLORS[index % AVATAR_COLORS.length]!;
</script>

<template>
  <div class="min-h-dvh flex flex-col items-center justify-center px-4">
    <div class="w-full max-w-sm space-y-10 text-center">
      <h1 class="text-3xl font-bold tracking-tight">Who's watching?</h1>

      <div
        class="grid justify-center gap-6"
        :class="users.length > 4 ? 'grid-cols-3' : 'grid-cols-2'"
      >
        <button
          v-for="(user, index) in users"
          :key="user.id"
          class="group flex flex-col items-center gap-2"
          data-testid="user-card"
          @click="handleSelect(user.id)"
        >
          <div
            class="flex size-20 items-center justify-center rounded-lg text-3xl transition-all group-hover:ring-2 group-hover:ring-green-500 group-active:scale-95"
            :class="user.icon ? 'bg-neutral-800' : colorForIndex(index)"
          >
            <span v-if="user.icon">{{ user.icon }}</span>
            <span v-else class="font-semibold text-white">
              {{ user.name.charAt(0).toUpperCase() }}
            </span>
          </div>
          <span class="text-sm text-neutral-300 group-hover:text-white transition-colors">
            {{ user.name }}
          </span>
        </button>
      </div>

      <UButton
        variant="ghost"
        color="neutral"
        icon="i-lucide-plus"
        data-testid="add-profile"
        @click="navigateTo('/settings')"
      >
        Add Profile
      </UButton>
    </div>
  </div>
</template>
