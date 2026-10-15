<script setup lang="ts">
definePageMeta({ layout: "blank" });

const route = useRoute();
const { connect } = useConnection();

const code = ref(typeof route.query.key === "string" ? route.query.key : "");
const loading = ref(false);
const error = ref<string | null>(null);

const pair = async () => {
  loading.value = true;
  error.value = null;
  try {
    await connect(code.value.trim());
    await navigateTo("/", { replace: true });
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : "Couldn't pair with that code";
  } finally {
    loading.value = false;
  }
};

// Scanning the TV's QR code opens this page with the code filled in.
onMounted(async () => {
  if (code.value) {
    await pair();
  }
});
</script>

<template>
  <div class="flex min-h-0 flex-1 items-center justify-center px-4">
    <div class="w-full max-w-sm space-y-6">
      <div class="text-center">
        <h1 class="text-3xl font-bold tracking-tight">Reel</h1>
        <p class="mt-2 text-neutral-400">Pair this phone with your TV to use it as a remote.</p>
      </div>

      <UAlert v-if="error" color="error" icon="i-lucide-alert-circle" :title="error" />

      <form class="space-y-4" @submit.prevent="pair">
        <UFormField
          label="Pairing code shown on your TV"
          help="Scanning the QR code on the TV with this phone fills it in for you."
        >
          <UInput
            v-model="code"
            autocomplete="off"
            autocapitalize="off"
            :spellcheck="false"
            icon="i-lucide-key-round"
          />
        </UFormField>

        <UButton type="submit" block :loading="loading" :disabled="!code.trim()">Pair</UButton>
      </form>
    </div>
  </div>
</template>
