<script setup lang="ts">
import type { User } from "~~/generated/rivet/types/domain";

const { currentUserId, users, fetchUsers, createUser, updateUser, deleteUser } = useUser();
const { serverUrl, isConnected, disconnect } = useConnection();

const currentUser = computed(() => users.value.find((u) => u.id === currentUserId.value));

// Avatar colours — same palette as select-user page
const AVATAR_COLORS = [
  "bg-green-700",
  "bg-blue-700",
  "bg-purple-700",
  "bg-amber-700",
  "bg-rose-700",
  "bg-cyan-700",
  "bg-orange-700",
  "bg-teal-700",
];

const colorForUser = (index: number) => AVATAR_COLORS[index % AVATAR_COLORS.length]!;

// Inline add user
const addingUser = ref(false);
const newUserName = ref("");
const creating = ref(false);

const startAdding = () => {
  cancelEdit();
  addingUser.value = true;
  newUserName.value = "";
};

const cancelAdding = () => {
  addingUser.value = false;
  newUserName.value = "";
};

const handleCreate = async () => {
  const name = newUserName.value.trim();
  if (!name) {
    return;
  }
  creating.value = true;
  try {
    await createUser(name);
    cancelAdding();
  } finally {
    creating.value = false;
  }
};

// Inline edit user
const editingUser = ref<number | null>(null);
const editName = ref("");
const editIcon = ref("");
const saving = ref(false);

const startEdit = (user: User) => {
  cancelAdding();
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
  if (editingUser.value === null) {
    return;
  }
  const name = editName.value.trim();
  if (!name) {
    return;
  }
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
const deleteError = ref<string | null>(null);

const confirmDelete = (user: User) => {
  deleteTarget.value = user;
  deleteError.value = null;
  deleteModalOpen.value = true;
};

const handleDelete = async () => {
  if (!deleteTarget.value) {
    return;
  }
  const deletedId = deleteTarget.value.id;
  deleting.value = true;
  deleteError.value = null;
  try {
    await deleteUser(deletedId);
    deleteModalOpen.value = false;
    deleteTarget.value = null;
  } catch (e) {
    deleteError.value = e instanceof Error ? e.message : "Failed to delete user";
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
    <!-- Profile card -->
    <section class="rounded-lg bg-neutral-900 p-4">
      <div class="flex items-center gap-4">
        <div
          class="flex size-16 shrink-0 items-center justify-center rounded-full text-2xl"
          :class="currentUser?.icon ? 'bg-neutral-800' : 'bg-green-700'"
          data-testid="profile-avatar"
        >
          <span v-if="currentUser?.icon">{{ currentUser.icon }}</span>
          <span v-else class="font-semibold text-white">
            {{ currentUser?.name.charAt(0).toUpperCase() ?? "?" }}
          </span>
        </div>
        <div>
          <p class="text-lg font-semibold" data-testid="profile-name">
            {{ currentUser?.name ?? "Unknown" }}
          </p>
          <button
            class="text-sm text-green-500 hover:text-green-400 transition-colors"
            data-testid="switch-user-link"
            @click="navigateTo('/select-user')"
          >
            Switch User
          </button>
        </div>
      </div>
    </section>

    <!-- Users section -->
    <section class="space-y-2">
      <h2 class="text-xs font-semibold text-neutral-500 uppercase tracking-wide px-1">Users</h2>

      <div class="rounded-lg bg-neutral-900 divide-y divide-neutral-800" data-testid="user-list">
        <div v-for="(user, index) in users" :key="user.id" class="px-4 py-3">
          <!-- Edit mode -->
          <template v-if="editingUser === user.id">
            <div class="flex items-center gap-2">
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
            </div>
          </template>

          <!-- Display mode -->
          <template v-else>
            <div class="flex items-center gap-3">
              <div
                class="flex size-8 shrink-0 items-center justify-center rounded-full text-sm"
                :class="user.icon ? 'bg-neutral-800' : colorForUser(index)"
              >
                <span v-if="user.icon">{{ user.icon }}</span>
                <span v-else class="font-semibold text-white">
                  {{ user.name.charAt(0).toUpperCase() }}
                </span>
              </div>
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
            </div>
          </template>
        </div>

        <!-- Add user row -->
        <div class="px-4 py-3">
          <template v-if="addingUser">
            <form class="flex items-center gap-2" @submit.prevent="handleCreate">
              <UInput
                v-model="newUserName"
                size="sm"
                placeholder="New user name..."
                class="flex-1"
                :disabled="creating"
                @keydown.escape="cancelAdding"
              />
              <UButton
                type="submit"
                size="sm"
                :loading="creating"
                :disabled="!newUserName.trim()"
              >
                Save
              </UButton>
              <UButton size="sm" variant="ghost" @click="cancelAdding">Cancel</UButton>
            </form>
          </template>
          <template v-else>
            <button
              class="flex items-center gap-2 text-sm text-neutral-400 hover:text-white transition-colors"
              data-testid="add-user-btn"
              @click="startAdding"
            >
              <UIcon name="i-lucide-plus" class="size-4" />
              Add User
            </button>
          </template>
        </div>
      </div>
    </section>

    <!-- Server section -->
    <section class="space-y-2">
      <h2 class="text-xs font-semibold text-neutral-500 uppercase tracking-wide px-1">Server</h2>

      <div class="rounded-lg bg-neutral-900 p-4 space-y-3" data-testid="server-section">
        <div class="space-y-2 text-sm">
          <div class="flex items-center gap-2">
            <span class="text-neutral-500">URL</span>
            <span class="text-neutral-300 font-mono truncate">
              {{ serverUrl || "Not connected" }}
            </span>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-neutral-500">Status</span>
            <span
              class="size-2 rounded-full"
              :class="isConnected ? 'bg-green-500' : 'bg-red-500'"
            />
            <span :class="isConnected ? 'text-green-500' : 'text-red-500'">
              {{ isConnected ? "Connected" : "Disconnected" }}
            </span>
          </div>
        </div>

        <UButton
          v-if="isConnected"
          variant="outline"
          color="error"
          block
          icon="i-lucide-log-out"
          data-testid="disconnect-btn"
          @click="handleDisconnect"
        >
          Disconnect
        </UButton>
      </div>
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
          <UAlert
            v-if="deleteError"
            color="error"
            icon="i-lucide-alert-circle"
            :title="deleteError"
          />
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
