<script setup lang="ts">
import { injectPlayback } from "~~/app/features/playback/ports/playback";

const { disconnect } = useConnection();
const { loading, error } = injectPlayback();
const status = computed(() =>
  loading.value
    ? { label: "Checking…", dot: "bg-neutral-500", text: "text-neutral-400" }
    : error.value
      ? { label: "Paired · can't reach the Studio", dot: "bg-red-500", text: "text-red-500" }
      : { label: "Paired", dot: "bg-green-500", text: "text-green-500" },
);

const handleDisconnect = () => {
  disconnect();
  navigateTo("/connect", { replace: true });
};
</script>

<template>
  <div class="space-y-6">
    <h1 class="page-title">Settings</h1>

    <section class="space-y-2">
      <h2 class="text-xs font-semibold text-neutral-500 uppercase tracking-wide px-1">Server</h2>
      <div class="rounded-lg bg-neutral-900 p-4 space-y-3">
        <div class="space-y-2 text-sm">
          <div class="flex items-center gap-2">
            <span class="text-neutral-500">Status</span>
            <span class="size-2 rounded-full" :class="status.dot" />
            <span :class="status.text">{{ status.label }}</span>
          </div>
        </div>

        <UButton
          variant="outline"
          color="error"
          block
          icon="i-lucide-log-out"
          @click="handleDisconnect"
        >
          Unpair this phone
        </UButton>
        <p class="text-xs text-neutral-500">You'll need the code on the TV to pair again.</p>
      </div>
    </section>
  </div>
</template>
