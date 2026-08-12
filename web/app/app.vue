<template>
  <UApp>
    <div class="safe-inset flex min-h-dvh flex-col">
      <NuxtLayout>
        <NuxtPage />
      </NuxtLayout>
    </div>
  </UApp>
</template>

<style>
.safe-inset {
   padding: env(safe-area-inset-top) env(safe-area-inset-right) 0 env(safe-area-inset-left);
  min-height: 100dvh;
}
</style>
