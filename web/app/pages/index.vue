<script setup lang="ts">
definePageMeta({
  layout: false,
  middleware: () => {
    const { isConnected } = useConnection();
    if (!isConnected.value) {
      return navigateTo("/connect", { replace: true });
    }

    const { hasValidCachedUser } = useUser();
    return navigateTo(hasValidCachedUser.value ? "/library" : "/select-user", { replace: true });
  },
});
</script>

<template>
  <div />
</template>
