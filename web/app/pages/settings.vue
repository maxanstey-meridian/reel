<script setup lang="ts">
import { injectPlayback } from "~~/app/features/playback/ports/playback";

const { disconnect } = useConnection();
const { loading, error } = injectPlayback();
const reachable = computed(() => !loading.value && !error.value);

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
            <span class="size-2 rounded-full" :class="reachable ? 'bg-green-500' : 'bg-red-500'" />
            <span :class="reachable ? 'text-green-500' : 'text-red-500'">
              {{ loading ? "Checking…" : error ? "Paired · server unavailable" : "Connected" }}
            </span>
          </div>
        </div>

        <UButton
          variant="outline"
          color="error"
          block
          icon="i-lucide-log-out"
          @click="handleDisconnect"
        >
          Disconnect
        </UButton>
      </div>
    </section>
  </div>
</template>
