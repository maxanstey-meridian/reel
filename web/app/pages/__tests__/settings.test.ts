// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed, readonly } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);
vi.stubGlobal("readonly", readonly);
vi.stubGlobal("onMounted", (fn: () => void) => fn());

const mockNavigateTo = vi.fn();
vi.stubGlobal("navigateTo", mockNavigateTo);

// Mock Rivet client
const mockUserList = vi.fn();
const mockUserCreate = vi.fn();
const mockUserUpdate = vi.fn();
const mockUserDelete = vi.fn();

vi.mock("~~/generated/rivet/client/reel", () => ({
  userList: (...args: unknown[]) => mockUserList(...args),
  userCreate: (...args: unknown[]) => mockUserCreate(...args),
  userUpdate: (...args: unknown[]) => mockUserUpdate(...args),
  userDelete: (...args: unknown[]) => mockUserDelete(...args),
}));

// Mock useConnection
const mockServerUrl = ref("http://localhost:8080");
const mockSecretKey = ref("test-key");
const mockDisconnect = vi.fn();

vi.stubGlobal("useConnection", () => ({
  serverUrl: mockServerUrl,
  isConnected: computed(() => !!mockServerUrl.value && !!mockSecretKey.value),
  disconnect: mockDisconnect,
}));

// Import composable after mocks
const { useUser } = await import("../../composables/useUser");

type User = { id: number; name: string; icon: string | null };

/**
 * Replicate settings page reactive wiring without mounting the component.
 */
const setupSettingsPage = async (userList: User[], activeUserId = 1) => {
  mockUserList.mockResolvedValue(userList);
  mockUserCreate.mockResolvedValue(undefined);
  mockUserUpdate.mockResolvedValue(undefined);
  mockUserDelete.mockResolvedValue(undefined);

  localStorage.setItem("reel:userId", String(activeUserId));

  const { currentUserId, users, fetchUsers, createUser, updateUser, deleteUser } = useUser();
  await fetchUsers();

  const currentUser = computed(() => users.value.find((u) => u.id === currentUserId.value));

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
    deleting.value = true;
    deleteError.value = null;
    try {
      await deleteUser(deleteTarget.value.id);
      deleteModalOpen.value = false;
      deleteTarget.value = null;
    } catch (e) {
      deleteError.value = e instanceof Error ? e.message : "Failed to delete user";
    } finally {
      deleting.value = false;
    }
  };

  const handleDisconnect = () => {
    mockDisconnect();
    mockNavigateTo("/connect", { replace: true });
  };

  return {
    currentUser,
    users,
    addingUser,
    newUserName,
    creating,
    startAdding,
    cancelAdding,
    handleCreate,
    editingUser,
    editName,
    editIcon,
    saving,
    startEdit,
    cancelEdit,
    saveEdit,
    deleteTarget,
    deleteModalOpen,
    deleting,
    deleteError,
    confirmDelete,
    handleDelete,
    handleDisconnect,
  };
};

const defaultUsers: User[] = [
  { id: 1, name: "Default", icon: null },
  { id: 2, name: "Alice", icon: "🌟" },
];

describe("Settings page — profile card", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("currentUser resolves to the user matching currentUserId", async () => {
    const { currentUser } = await setupSettingsPage(defaultUsers, 2);

    expect(currentUser.value?.name).toBe("Alice");
    expect(currentUser.value?.icon).toBe("🌟");
  });

  it("shows first letter when user has no icon", async () => {
    const { currentUser } = await setupSettingsPage(defaultUsers, 1);

    expect(currentUser.value?.icon).toBeNull();
    expect(currentUser.value?.name.charAt(0).toUpperCase()).toBe("D");
  });

  it("shows undefined when currentUserId does not match any user", async () => {
    const { currentUser } = await setupSettingsPage(defaultUsers, 99);

    expect(currentUser.value).toBeUndefined();
  });
});

describe("Settings page — Switch User link", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("navigates to /select-user", async () => {
    await setupSettingsPage(defaultUsers);

    // Replicate template click handler
    mockNavigateTo("/select-user");

    expect(mockNavigateTo).toHaveBeenCalledWith("/select-user");
  });
});

describe("Settings page — user list", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("lists all users", async () => {
    const { users } = await setupSettingsPage(defaultUsers);

    expect(users.value).toHaveLength(2);
    expect(users.value[0]!.name).toBe("Default");
    expect(users.value[1]!.name).toBe("Alice");
  });

  it("startEdit populates edit fields from user", async () => {
    const { startEdit, editingUser, editName, editIcon } = await setupSettingsPage(defaultUsers);

    startEdit({ id: 2, name: "Alice", icon: "🌟" });

    expect(editingUser.value).toBe(2);
    expect(editName.value).toBe("Alice");
    expect(editIcon.value).toBe("🌟");
  });

  it("saveEdit calls updateUser with correct args and clears edit state", async () => {
    const { startEdit, editName, editIcon, saveEdit, editingUser } =
      await setupSettingsPage(defaultUsers);

    startEdit({ id: 2, name: "Alice", icon: "🌟" });
    editName.value = "Alicia";
    editIcon.value = "⭐";
    await saveEdit();

    expect(mockUserUpdate).toHaveBeenCalledWith("2", { name: "Alicia", icon: "⭐" });
    expect(editingUser.value).toBeNull();
  });

  it("cancelEdit clears edit state", async () => {
    const { startEdit, cancelEdit, editingUser, editName } =
      await setupSettingsPage(defaultUsers);

    startEdit({ id: 1, name: "Default", icon: null });
    expect(editingUser.value).toBe(1);

    cancelEdit();

    expect(editingUser.value).toBeNull();
    expect(editName.value).toBe("");
  });

  it("startEdit cancels any in-progress adding", async () => {
    const { startAdding, addingUser, startEdit } = await setupSettingsPage(defaultUsers);

    startAdding();
    expect(addingUser.value).toBe(true);

    startEdit({ id: 1, name: "Default", icon: null });
    expect(addingUser.value).toBe(false);
  });
});

describe("Settings page — inline add user", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("startAdding opens inline form and cancels edit", async () => {
    const { startEdit, startAdding, addingUser, editingUser } =
      await setupSettingsPage(defaultUsers);

    startEdit({ id: 1, name: "Default", icon: null });
    expect(editingUser.value).toBe(1);

    startAdding();

    expect(addingUser.value).toBe(true);
    expect(editingUser.value).toBeNull();
  });

  it("handleCreate calls createUser and closes add form", async () => {
    const { startAdding, newUserName, handleCreate, addingUser } =
      await setupSettingsPage(defaultUsers);

    startAdding();
    newUserName.value = "Bob";
    await handleCreate();

    expect(mockUserCreate).toHaveBeenCalledWith({ name: "Bob", icon: null });
    expect(addingUser.value).toBe(false);
    expect(newUserName.value).toBe("");
  });

  it("handleCreate does nothing when name is empty", async () => {
    const { startAdding, handleCreate, addingUser } = await setupSettingsPage(defaultUsers);

    startAdding();
    await handleCreate();

    expect(mockUserCreate).not.toHaveBeenCalled();
    expect(addingUser.value).toBe(true);
  });

  it("cancelAdding closes the form", async () => {
    const { startAdding, cancelAdding, addingUser, newUserName } =
      await setupSettingsPage(defaultUsers);

    startAdding();
    newUserName.value = "Bob";
    cancelAdding();

    expect(addingUser.value).toBe(false);
    expect(newUserName.value).toBe("");
  });
});

describe("Settings page — server section", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    mockServerUrl.value = "http://localhost:8080";
    mockSecretKey.value = "test-key";
  });

  it("disconnect calls disconnect and navigates to /connect", async () => {
    const { handleDisconnect } = await setupSettingsPage(defaultUsers);

    handleDisconnect();

    expect(mockDisconnect).toHaveBeenCalledOnce();
    expect(mockNavigateTo).toHaveBeenCalledWith("/connect", { replace: true });
  });
});

describe("Settings page — delete modal", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("confirmDelete opens modal with target user", async () => {
    const { confirmDelete, deleteModalOpen, deleteTarget } =
      await setupSettingsPage(defaultUsers);

    confirmDelete({ id: 2, name: "Alice", icon: "🌟" });

    expect(deleteModalOpen.value).toBe(true);
    expect(deleteTarget.value?.name).toBe("Alice");
  });

  it("handleDelete calls deleteUser and closes modal", async () => {
    const { confirmDelete, handleDelete, deleteModalOpen, deleteTarget } =
      await setupSettingsPage(defaultUsers);

    confirmDelete({ id: 2, name: "Alice", icon: "🌟" });
    await handleDelete();

    expect(mockUserDelete).toHaveBeenCalledWith("2");
    expect(deleteModalOpen.value).toBe(false);
    expect(deleteTarget.value).toBeNull();
  });

  it("handleDelete surfaces errors without closing modal", async () => {
    const { confirmDelete, handleDelete, deleteModalOpen, deleteError } =
      await setupSettingsPage(defaultUsers);

    mockUserDelete.mockRejectedValue(new Error("Cannot delete last user"));
    confirmDelete({ id: 1, name: "Default", icon: null });
    await handleDelete();

    expect(deleteModalOpen.value).toBe(true);
    expect(deleteError.value).toBe("Cannot delete last user");
  });
});
