<script setup lang="ts">
definePageMeta({ layout: "blank" });

const route = useRoute();
const { connect } = useConnection();

const url = ref((route.query.url as string) || "http://localhost:8080");
const key = ref((route.query.key as string) || "");
const loading = ref(false);
const error = ref<string | null>(null);

const handleConnect = async () => {
  loading.value = true;
  error.value = null;

  try {
    await connect(url.value, key.value);
    const { fetchUsers } = useUser();
    await fetchUsers();
    await navigateTo("/", { replace: true });
  } catch (e) {
    error.value = e instanceof Error ? e.message : "Connection failed";
  } finally {
    loading.value = false;
  }
};

// Auto-connect when both params are provided (QR code flow)
onMounted(async () => {
  if (route.query.url && route.query.key) {
    await handleConnect();
  }
});
</script>

<template>
  <div class="min-h-dvh flex items-center justify-center px-4">
    <div class="w-full max-w-sm space-y-6">
      <div class="text-center">
        <h1 class="text-3xl font-bold tracking-tight">Reel</h1>
        <p class="mt-2 text-neutral-400">Connect to your media server</p>
      </div>

      <UAlert v-if="error" color="error" icon="i-lucide-alert-circle" :title="error" />

      <form class="space-y-4" @submit.prevent="handleConnect">
        <UFormField label="Server URL">
          <UInput v-model="url" placeholder="http://192.168.1.x:8080" icon="i-lucide-server" />
        </UFormField>

        <UFormField label="Secret Key">
          <UInput
            v-model="key"
            type="text"
            placeholder="Enter secret key"
            icon="i-lucide-key-round"
          />
        </UFormField>

        <UButton type="submit" block :loading="loading" :disabled="!url || !key"> Connect </UButton>
      </form>
    </div>
  </div>
</template>
