<script setup lang="ts">
definePageMeta({
  layout: false,
  middleware: () => {
    const { isConnected } = useConnection();
    return navigateTo(isConnected.value ? "/library" : "/connect", { replace: true });
  },
});
</script>

<template>
  <div />
</template>
