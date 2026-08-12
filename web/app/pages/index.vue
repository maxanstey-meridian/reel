<script setup lang="ts">
definePageMeta({
  layout: false,
  middleware: () => {
    const { isConnected } = useConnection();
    if (!isConnected.value) {
      return navigateTo("/connect", { replace: true });
    }

    return navigateTo("/library", { replace: true });
  },
});
</script>

<template>
  <div />
</template>
