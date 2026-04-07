<script setup lang="ts">
const config = useRuntimeConfig();
const serverUrl = config.public.serverUrl;
</script>

<template>
  <div class="min-h-screen bg-neutral-950 text-white flex items-center justify-center">
    <div class="text-center space-y-lg">
      <h1 class="text-3xl font-bold tracking-tight">Reel</h1>
      <p class="text-neutral-400">Connecting to {{ serverUrl }}</p>
    </div>
  </div>
</template>
