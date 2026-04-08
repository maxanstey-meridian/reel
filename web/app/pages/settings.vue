<script setup lang="ts">
import { ref } from "vue";
import type { User } from "~~/generated/rivet/types/domain";

const { currentUserId, users, fetchUsers, switchUser, createUser, updateUser, deleteUser } =
  useUser();
const { serverUrl, isConnected, disconnect } = useConnection();
const { refresh: refreshLibrary } = useLibrary();
const { fetch: fetchProgress } = useProgress();
const { fetch: fetchFavourites } = useFavourites();

// Create user form
const newUserName = ref("");
const creating = ref(false);

const handleCreate = async () => {
  const name = newUserName.value.trim();
  if (!name) return;
  creating.value = true;
  try {
    await createUser(name);
    newUserName.value = "";
  } finally {
    creating.value = false;
  }
};

// Switch user
const switching = ref(false);

const handleSwitch = async (id: number) => {
  if (id === currentUserId.value) return;
  switching.value = true;
  try {
    switchUser(id);
    await Promise.all([refreshLibrary(), fetchProgress(), fetchFavourites()]);
  } finally {
    switching.value = false;
  }
};

// Edit user
const editingUser = ref<number | null>(null);
const editName = ref("");
const editIcon = ref("");
const saving = ref(false);

const startEdit = (user: User) => {
  editingUser.value = user.id;
  editName.value = user.name;
  editIcon.value = user.icon ?? "";
};

const cancelEdit = () => {
  editingUser.value = null;
  editName.value = "";
  editIcon.value = "";
};

const saveEdit = async () => {
  if (editingUser.value === null) return;
  const name = editName.value.trim();
  if (!name) return;
  saving.value = true;
  try {
    await updateUser(editingUser.value, name, editIcon.value.trim() || undefined);
    cancelEdit();
  } finally {
    saving.value = false;
  }
};

// Delete user
const deleteTarget = ref<User | null>(null);
const deleteModalOpen = ref(false);
const deleting = ref(false);

const confirmDelete = (user: User) => {
  deleteTarget.value = user;
  deleteModalOpen.value = true;
};

const handleDelete = async () => {
  if (!deleteTarget.value) return;
  deleting.value = true;
  try {
    await deleteUser(deleteTarget.value.id);
    deleteModalOpen.value = false;
    deleteTarget.value = null;
  } finally {
    deleting.value = false;
  }
};

// Disconnect
const handleDisconnect = () => {
  disconnect();
  navigateTo("/connect", { replace: true });
};

onMounted(() => {
  fetchUsers();
});
</script>

<template>
  <div class="space-y-6">
    <div class="page-header">
      <h1 class="page-title">Settings</h1>
    </div>

    <!-- Users section -->
    <section class="space-y-3">
      <h2 class="text-sm font-medium text-neutral-400">Users</h2>

      <!-- User list / picker -->
      <div class="space-y-1" data-testid="user-list">
        <button
          v-for="user in users"
          :key="user.id"
          class="w-full flex items-center gap-3 px-3 py-2.5 rounded transition-colors"
          :class="
            user.id === currentUserId
              ? 'bg-neutral-800 text-green-500'
              : 'text-neutral-300 hover:bg-neutral-900'
          "
          :disabled="switching"
          @click="handleSwitch(user.id)"
        >
          <span v-if="user.icon" class="text-lg">{{ user.icon }}</span>
          <UIcon v-else name="i-lucide-user" class="size-5 shrink-0" />
          <span class="flex-1 text-left text-sm truncate">{{ user.name }}</span>
          <UIcon
            v-if="user.id === currentUserId"
            name="i-lucide-check"
            class="size-4 shrink-0"
          />
        </button>
      </div>

      <!-- User management for each user -->
      <div class="space-y-2 pt-2">
        <div v-for="user in users" :key="user.id" class="flex items-center gap-2">
          <template v-if="editingUser === user.id">
            <UInput
              v-model="editName"
              size="sm"
              placeholder="Name"
              class="flex-1"
              :disabled="saving"
              @keydown.enter="saveEdit"
              @keydown.escape="cancelEdit"
            />
            <UInput
              v-model="editIcon"
              size="sm"
              placeholder="Icon"
              class="w-16"
              :disabled="saving"
              @keydown.enter="saveEdit"
              @keydown.escape="cancelEdit"
            />
            <UButton size="sm" :loading="saving" @click="saveEdit">Save</UButton>
            <UButton size="sm" variant="ghost" @click="cancelEdit">Cancel</UButton>
          </template>
          <template v-else>
            <span class="flex-1 text-sm text-neutral-300 truncate">{{ user.name }}</span>
            <UButton
              size="xs"
              variant="ghost"
              color="neutral"
              icon="i-lucide-pencil"
              @click="startEdit(user)"
            />
            <UButton
              size="xs"
              variant="ghost"
              color="error"
              icon="i-lucide-trash-2"
              @click="confirmDelete(user)"
            />
          </template>
        </div>
      </div>

      <!-- Create user -->
      <form class="flex items-center gap-2 pt-2" @submit.prevent="handleCreate">
        <UInput
          v-model="newUserName"
          size="sm"
          placeholder="New user name..."
          class="flex-1"
          :disabled="creating"
        />
        <UButton
          type="submit"
          size="sm"
          icon="i-lucide-plus"
          :loading="creating"
          :disabled="!newUserName.trim()"
        >
          Add
        </UButton>
      </form>
    </section>

    <!-- Server connection section -->
    <section class="space-y-3">
      <h2 class="text-sm font-medium text-neutral-400">Server</h2>

      <div class="space-y-2 text-sm">
        <div class="flex items-center gap-2">
          <span class="text-neutral-500">URL</span>
          <span class="text-neutral-300 truncate">{{ serverUrl || "Not connected" }}</span>
        </div>
        <div class="flex items-center gap-2">
          <span class="text-neutral-500">Status</span>
          <span :class="isConnected ? 'text-green-500' : 'text-red-500'">
            {{ isConnected ? "Connected" : "Disconnected" }}
          </span>
        </div>
      </div>

      <UButton
        v-if="isConnected"
        variant="outline"
        color="error"
        size="sm"
        icon="i-lucide-log-out"
        @click="handleDisconnect"
      >
        Disconnect
      </UButton>
    </section>

    <!-- Delete confirmation modal -->
    <UModal v-model:open="deleteModalOpen">
      <template #content>
        <div class="p-6 space-y-4">
          <h3 class="text-lg font-semibold">Delete User</h3>
          <p class="text-sm text-neutral-400">
            Are you sure you want to delete
            <strong class="text-white">{{ deleteTarget?.name }}</strong
            >? This will permanently delete all their data including progress, favourites, tags, and
            watched state.
          </p>
          <div class="flex justify-end gap-2">
            <UButton variant="ghost" @click="deleteModalOpen = false">Cancel</UButton>
            <UButton color="error" :loading="deleting" @click="handleDelete">
              Delete User
            </UButton>
          </div>
        </div>
      </template>
    </UModal>
  </div>
</template>
