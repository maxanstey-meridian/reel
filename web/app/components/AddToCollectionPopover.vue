<script setup lang="ts">
import { ref } from "vue";
import { useCollections } from "~~/app/composables/useCollections";

const props = defineProps<{
  relativePath: string;
  /** Popover content placement — defaults to bottom/start */
  content?: Record<string, string>;
}>();

const emit = defineEmits<{ added: [] }>();

const { collections, fetch: fetchCollections, addItem, create: createCollection } = useCollections();

const open = ref(false);
const newName = ref("");
const creating = ref(false);

const openPopover = async () => {
  if (collections.value.length === 0) {
    await fetchCollections();
  }
  open.value = true;
};

const handleAdd = async (collectionId: number) => {
  await addItem(collectionId, props.relativePath);
  open.value = false;
  emit("added");
};

const handleCreateAndAdd = async () => {
  const name = newName.value.trim();
  if (!name) return;
  creating.value = true;
  try {
    const created = await createCollection(name);
    if (created) {
      await addItem(created.id, props.relativePath);
    }
    newName.value = "";
    open.value = false;
    emit("added");
  } finally {
    creating.value = false;
  }
};

defineExpose({ openPopover });
</script>

<template>
  <UPopover v-model:open="open" :content="content">
    <slot :open-popover="openPopover" />
    <template #content>
      <div class="p-2 w-48 space-y-1" @click.stop>
        <p class="text-xs text-neutral-500 px-1 mb-1">Add to collection</p>
        <button
          v-for="c in collections"
          :key="c.id"
          class="w-full text-left text-sm px-2 py-1.5 rounded hover:bg-neutral-800 text-neutral-300 hover:text-white transition-colors truncate"
          @click="handleAdd(c.id)"
        >
          {{ c.name }}
        </button>
        <div v-if="collections.length === 0" class="text-xs text-neutral-500 px-2 py-1">
          No collections yet
        </div>
        <div class="border-t border-neutral-800 pt-1 mt-1">
          <form class="flex items-center gap-1" @submit.prevent="handleCreateAndAdd">
            <input
              v-model="newName"
              type="text"
              placeholder="New collection..."
              class="flex-1 text-xs bg-transparent border border-neutral-700 rounded px-2 py-1 text-neutral-300 placeholder-neutral-600 outline-none focus:border-green-500"
              :disabled="creating"
            />
            <button
              type="submit"
              class="text-xs text-green-500 hover:text-green-400 px-1 disabled:opacity-50"
              :disabled="!newName.trim() || creating"
            >
              Add
            </button>
          </form>
        </div>
      </div>
    </template>
  </UPopover>
</template>
