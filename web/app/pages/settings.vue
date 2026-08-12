<script setup lang="ts">
const { serverUrl, isConnected, disconnect } = useConnection();

const handleDisconnect = () => {
  disconnect();
  navigateTo("/connect", { replace: true });
};
</script>

<template>
  <div class="space-y-6">
    <div class="page-header">
      <h1 class="page-title">Settings</h1>
    </div>

    <section class="space-y-2">
      <h2 class="text-xs font-semibold text-neutral-500 uppercase tracking-wide px-1">Server</h2>
      <div class="rounded-lg bg-neutral-900 p-4 space-y-3" data-testid="server-section">
        <div class="space-y-2 text-sm">
          <div class="flex items-center gap-2">
            <span class="text-neutral-500">URL</span>
            <span class="text-neutral-300 font-mono truncate">{{ serverUrl || "Not connected" }}</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-neutral-500">Status</span>
            <span class="size-2 rounded-full" :class="isConnected ? 'bg-green-500' : 'bg-red-500'" />
            <span :class="isConnected ? 'text-green-500' : 'text-red-500'">
              {{ isConnected ? "Connected" : "Disconnected" }}
            </span>
          </div>
        </div>

        <UButton
          v-if="isConnected"
          variant="outline"
          color="error"
          block
          icon="i-lucide-log-out"
          data-testid="disconnect-btn"
          @click="handleDisconnect"
        >
          Disconnect
        </UButton>
      </div>
    </section>
  </div>
</template>
