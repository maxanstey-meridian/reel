<script setup lang="ts">
import { useConnection } from "~~/app/composables/useConnection";
import { useCollections } from "~~/app/composables/useCollections";
import { useCast } from "~~/app/composables/useCast";
import { parseFilename } from "~~/app/utils/parseFilename";
import { thumbnailUrl } from "~~/generated/rivet/client/reel";
import type { CollectionWithItems } from "~~/generated/rivet/types/domain";

const route = useRoute();
const router = useRouter();
const { secretKey } = useConnection();
const { remove, removeItem, rename, getWithItems } = useCollections();
const { castQueue, casting } = useCast();

const collectionId = computed(() => String(route.params.id));

const collection = ref<CollectionWithItems | null>(null);
const loading = ref(false);

// Inline rename
const editing = ref(false);
const editName = ref("");
const saving = ref(false);

const startEdit = () => {
  if (!collection.value) return;
  editing.value = true;
  editName.value = collection.value.name;
};

const cancelEdit = () => {
  editing.value = false;
  editName.value = "";
};

const saveEdit = async () => {
  if (!collection.value) return;
  const name = editName.value.trim();
  if (!name || name === collection.value.name) {
    cancelEdit();
    return;
  }
  saving.value = true;
  try {
    await rename(collection.value.id, name);
    collection.value = { ...collection.value, name };
    cancelEdit();
  } finally {
    saving.value = false;
  }
};

// Delete
const deleteModalOpen = ref(false);
const deleting = ref(false);

const confirmDelete = () => {
  deleteModalOpen.value = true;
};

const handleDelete = async () => {
  if (!collection.value) return;
  deleting.value = true;
  try {
    await remove(collection.value.id);
    deleteModalOpen.value = false;
    navigateTo("/collections", { replace: true });
  } finally {
    deleting.value = false;
  }
};

// Remove item
const handleRemoveItem = async (relativePath: string) => {
  if (!collection.value) return;
  await removeItem(collection.value.id, relativePath);
  collection.value = {
    ...collection.value,
    items: collection.value.items.filter((i) => i.relativePath !== relativePath),
  };
};

const handleCastAll = async () => {
  if (!collection.value || collection.value.items.length === 0) return;
  const paths = collection.value.items.map((i) => i.relativePath);
  await castQueue(paths);
  navigateTo("/now-playing");
};

// Helpers
const fileNameFromPath = (path: string) => {
  const parts = path.split("/");
  return parts[parts.length - 1] ?? path;
};

const displayTitle = (path: string) => {
  const name = fileNameFromPath(path);
  const parsed = parseFilename(name);
  return parsed.title;
};

const itemThumbnail = (relativePath: string) => {
  if (!secretKey.value) return null;
  return thumbnailUrl(relativePath, secretKey.value);
};

// Load
const fetchCollection = async () => {
  loading.value = true;
  try {
    collection.value = await getWithItems(collectionId.value);
  } finally {
    loading.value = false;
  }
};

onMounted(fetchCollection);
</script>

<template>
  <div class="space-y-4">
    <!-- Header -->
    <div class="flex items-center gap-2">
      <UButton
        variant="ghost"
        color="neutral"
        size="sm"
        icon="i-lucide-arrow-left"
        @click="router.back()"
      />

      <!-- Editable name -->
      <template v-if="editing">
        <UInput
          v-model="editName"
          size="sm"
          class="flex-1"
          :disabled="saving"
          @keydown.enter="saveEdit"
          @keydown.escape="cancelEdit"
          @blur="saveEdit"
        />
      </template>
      <template v-else>
        <button
          class="flex-1 text-left text-base font-semibold truncate flex items-center gap-1 hover:text-neutral-300 transition-colors"
          data-testid="collection-name"
          @click="startEdit"
        >
          {{ collection?.name ?? "..." }}
          <span class="i-lucide-pencil size-3 text-neutral-500 shrink-0" />
        </button>
      </template>

      <UButton
        variant="ghost"
        color="error"
        size="sm"
        icon="i-lucide-trash-2"
        data-testid="delete-collection-btn"
        @click="confirmDelete"
      />
    </div>

    <!-- Loading -->
    <div v-if="loading" class="space-y-3">
      <USkeleton v-for="i in 4" :key="i" class="h-16 w-full rounded" />
    </div>

    <template v-else-if="collection">
      <!-- Item count -->
      <p class="text-xs text-neutral-500">
        {{ collection.items.length }} {{ collection.items.length === 1 ? "item" : "items" }}
      </p>

      <!-- Empty collection -->
      <div
        v-if="collection.items.length === 0"
        class="flex flex-col items-center justify-center py-12 text-neutral-500"
      >
        <UIcon name="i-lucide-list-video" class="size-10 mb-3" />
        <p class="text-sm font-medium">No items yet</p>
        <NuxtLink
          to="/library"
          class="text-xs text-green-500 hover:text-green-400 mt-2 transition-colors"
        >
          Browse library to add files
        </NuxtLink>
      </div>

      <!-- Items list -->
      <div v-else class="space-y-1">
        <div
          v-for="(item, index) in collection.items"
          :key="item.relativePath"
          class="flex items-center gap-3 rounded-lg bg-neutral-900 p-3"
          data-testid="collection-item"
        >
          <span class="text-xs text-neutral-600 w-5 text-right shrink-0">{{ index + 1 }}</span>

          <!-- Thumbnail -->
          <div class="size-10 rounded bg-neutral-800 overflow-hidden shrink-0 flex items-center justify-center">
            <img
              v-if="itemThumbnail(item.relativePath)"
              :src="itemThumbnail(item.relativePath) ?? ''"
              class="w-full h-full object-cover"
              loading="lazy"
              @error="$event.target instanceof HTMLImageElement && ($event.target.style.display = 'none')"
            />
            <UIcon v-else name="i-lucide-film" class="size-5 text-neutral-500" />
          </div>

          <!-- Title -->
          <NuxtLink
            :to="`/detail/${encodeURIComponent(item.relativePath)}`"
            class="flex-1 min-w-0 text-sm text-neutral-300 truncate hover:text-white transition-colors"
          >
            {{ displayTitle(item.relativePath) }}
          </NuxtLink>

          <!-- Remove -->
          <button
            class="p-1 text-neutral-600 hover:text-red-400 transition-colors shrink-0"
            data-testid="remove-item-btn"
            @click="handleRemoveItem(item.relativePath)"
          >
            <UIcon name="i-lucide-x" class="size-4" />
          </button>
        </div>
      </div>

      <!-- Cast All -->
      <UButton
        v-if="collection.items.length > 0"
        block
        size="lg"
        icon="i-lucide-cast"
        :loading="casting"
        data-testid="cast-all-btn"
        @click="handleCastAll"
      >
        Cast All
      </UButton>

      <!-- Add files link -->
      <NuxtLink
        to="/library"
        class="flex items-center justify-center gap-1 text-sm text-neutral-400 hover:text-white transition-colors py-2"
      >
        <UIcon name="i-lucide-plus" class="size-4" />
        Add files from library
      </NuxtLink>
    </template>

    <!-- Delete confirmation modal -->
    <UModal v-model:open="deleteModalOpen">
      <template #content>
        <div class="p-6 space-y-4">
          <h3 class="text-lg font-semibold">Delete Collection</h3>
          <p class="text-sm text-neutral-400">
            Are you sure you want to delete
            <strong class="text-white">{{ collection?.name }}</strong>?
            This will remove the collection but not the media files.
          </p>
          <div class="flex justify-end gap-2">
            <UButton variant="ghost" @click="deleteModalOpen = false">Cancel</UButton>
            <UButton color="error" :loading="deleting" @click="handleDelete">
              Delete Collection
            </UButton>
          </div>
        </div>
      </template>
    </UModal>
  </div>
</template>
