<script setup lang="ts">
const { isConnected } = useConnection();

await navigateTo(isConnected.value ? "/library" : "/connect", { replace: true });
</script>

<template>
  <div />
</template>
