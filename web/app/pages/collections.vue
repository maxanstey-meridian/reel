<script setup lang="ts">
import { useConnection } from "../composables/useConnection";
import { useCollections } from "../composables/useCollections";

const { isConnected } = useConnection();
const { collections, loading, error, fetch: fetchCollections, create } = useCollections();

// Inline create
const adding = ref(false);
const newName = ref("");
const creating = ref(false);

const startAdding = () => {
  adding.value = true;
  newName.value = "";
};

const cancelAdding = () => {
  adding.value = false;
  newName.value = "";
};

const handleCreate = async () => {
  const name = newName.value.trim();
  if (!name) return;

  creating.value = true;
  try {
    const created = await create(name);
    cancelAdding();
    if (created) {
      navigateTo(`/collections/${created.id}`);
    }
  } finally {
    creating.value = false;
  }
};

onMounted(() => {
  if (!isConnected.value) {
    navigateTo("/connect", { replace: true });
    return;
  }
  fetchCollections();
});
</script>

<template>
  <div class="space-y-4">
    <div class="page-header flex items-center justify-between">
      <h1 class="page-title">Collections</h1>
      <UButton
        v-if="!adding"
        size="xs"
        variant="subtle"
        icon="i-lucide-plus"
        data-testid="new-collection-btn"
        @click="startAdding"
      >
        New
      </UButton>
    </div>

    <!-- Inline create -->
    <form v-if="adding" class="flex items-center gap-2" @submit.prevent="handleCreate">
      <UInput
        v-model="newName"
        size="sm"
        placeholder="Collection name..."
        class="flex-1"
        :disabled="creating"
        @keydown.escape="cancelAdding"
      />
      <UButton
        type="submit"
        size="sm"
        :loading="creating"
        :disabled="!newName.trim()"
      >
        Save
      </UButton>
      <UButton size="sm" variant="ghost" @click="cancelAdding">Cancel</UButton>
    </form>

    <!-- Error -->
    <UAlert v-if="error" color="error" icon="i-lucide-alert-circle" :title="error" />

    <!-- Loading -->
    <div v-if="loading" class="space-y-3">
      <USkeleton v-for="i in 3" :key="i" class="h-16 w-full rounded-lg" />
    </div>

    <!-- Empty state -->
    <div
      v-else-if="collections.length === 0 && !adding"
      class="flex flex-col items-center justify-center py-16 text-neutral-500"
    >
      <UIcon name="i-lucide-list-video" class="size-12 mb-3" />
      <p class="text-sm font-medium">No collections yet</p>
      <p class="text-xs text-neutral-600 mt-1">Create a collection to organise your media</p>
      <UButton
        size="sm"
        variant="subtle"
        icon="i-lucide-plus"
        class="mt-4"
        @click="startAdding"
      >
        New Collection
      </UButton>
    </div>

    <!-- Collections list -->
    <div v-else class="space-y-2">
      <button
        v-for="collection in collections"
        :key="collection.id"
        class="w-full flex items-center gap-3 rounded-lg bg-neutral-900 p-4 text-left transition-all hover:ring-1 hover:ring-green-500 focus-visible:ring-1 focus-visible:ring-green-500 outline-none"
        data-testid="collection-card"
        @click="navigateTo(`/collections/${collection.id}`)"
      >
        <UIcon name="i-lucide-list-video" class="size-8 text-neutral-500 shrink-0" />
        <div class="flex-1 min-w-0">
          <p class="text-sm font-medium truncate">{{ collection.name }}</p>
          <p class="text-xs text-neutral-500">
            {{ collection.itemCount }} {{ collection.itemCount === 1 ? "item" : "items" }}
          </p>
        </div>
        <UIcon name="i-lucide-chevron-right" class="size-4 text-neutral-600 shrink-0" />
      </button>
    </div>
  </div>
</template>
