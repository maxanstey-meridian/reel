// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, readonly, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("readonly", readonly);
vi.stubGlobal("computed", computed);

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

// Import after mocks
const { useUser } = await import("../useUser");

describe("useUser", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    mockUserList.mockResolvedValue([]);
    mockUserCreate.mockResolvedValue({ id: 2, name: "Alice", icon: null });
    mockUserUpdate.mockResolvedValue({ id: 1, name: "Updated", icon: null });
    mockUserDelete.mockResolvedValue(undefined);
  });

  it("defaults currentUserId to 1 when localStorage is empty", () => {
    const { currentUserId } = useUser();
    expect(currentUserId.value).toBe(1);
  });

  it("reads persisted userId from localStorage on init", () => {
    localStorage.setItem("reel:userId", "3");

    // Need a fresh useState call to pick up localStorage
    // Since useState is mocked to call init() every time, this works
    const { currentUserId } = useUser();
    expect(currentUserId.value).toBe(3);
  });

  it("switchUser updates currentUserId and persists to localStorage", () => {
    const { switchUser, currentUserId } = useUser();

    switchUser(5);

    expect(currentUserId.value).toBe(5);
    expect(localStorage.getItem("reel:userId")).toBe("5");
  });

  it("fetchUsers calls userList and populates users ref", async () => {
    const mockUsers = [
      { id: 1, name: "Default", icon: null },
      { id: 2, name: "Alice", icon: "star" },
    ];
    mockUserList.mockResolvedValue(mockUsers);

    const { fetchUsers, users } = useUser();
    await fetchUsers();

    expect(mockUserList).toHaveBeenCalledOnce();
    expect(users.value).toEqual(mockUsers);
  });

  it("createUser calls userCreate with correct body and re-fetches users", async () => {
    const { createUser } = useUser();

    await createUser("Bob", "rocket");

    expect(mockUserCreate).toHaveBeenCalledWith({ name: "Bob", icon: "rocket" });
    expect(mockUserList).toHaveBeenCalledOnce();
  });

  it("createUser passes null for icon when not provided", async () => {
    const { createUser } = useUser();

    await createUser("Bob");

    expect(mockUserCreate).toHaveBeenCalledWith({ name: "Bob", icon: null });
  });

  it("updateUser calls userUpdate with stringified id and re-fetches users", async () => {
    const { updateUser } = useUser();

    await updateUser(3, "New Name", "star");

    expect(mockUserUpdate).toHaveBeenCalledWith("3", { name: "New Name", icon: "star" });
    expect(mockUserList).toHaveBeenCalledOnce();
  });

  it("deleteUser calls userDelete with stringified id and re-fetches users", async () => {
    const { deleteUser } = useUser();

    await deleteUser(2);

    expect(mockUserDelete).toHaveBeenCalledWith("2");
    expect(mockUserList).toHaveBeenCalledOnce();
  });

  it("after deleting current user, switchUser can switch to a remaining user", async () => {
    mockUserList.mockResolvedValue([
      { id: 1, name: "Default", icon: null },
      { id: 2, name: "Alice", icon: null },
    ]);

    const { deleteUser, switchUser, currentUserId, users } = useUser();
    switchUser(2);
    expect(currentUserId.value).toBe(2);

    // Simulate deletion — after delete, useUser re-fetches and only user 1 remains
    mockUserList.mockResolvedValue([{ id: 1, name: "Default", icon: null }]);
    await deleteUser(2);

    // users list is now updated
    expect(users.value).toEqual([{ id: 1, name: "Default", icon: null }]);

    // Page would switch to first remaining user
    const remaining = users.value[0]!;
    switchUser(remaining.id);
    expect(currentUserId.value).toBe(1);
    expect(localStorage.getItem("reel:userId")).toBe("1");
  });

  it("deleteUser surfaces errors to the caller", async () => {
    mockUserDelete.mockRejectedValue(new Error("Cannot delete last user"));

    const { deleteUser } = useUser();

    await expect(deleteUser(1)).rejects.toThrow("Cannot delete last user");
  });

  describe("hasValidCachedUser", () => {
    it("returns true when currentUserId matches a user in the list", async () => {
      mockUserList.mockResolvedValue([
        { id: 1, name: "Default", icon: null },
        { id: 2, name: "Alice", icon: null },
      ]);

      const { fetchUsers, hasValidCachedUser, switchUser } = useUser();
      await fetchUsers();
      switchUser(2);

      expect(hasValidCachedUser.value).toBe(true);
    });

    it("returns false when currentUserId does not match any user", async () => {
      mockUserList.mockResolvedValue([
        { id: 1, name: "Default", icon: null },
      ]);

      const { fetchUsers, hasValidCachedUser, switchUser } = useUser();
      await fetchUsers();
      switchUser(99);

      expect(hasValidCachedUser.value).toBe(false);
    });

    it("returns false when users list is empty", () => {
      const { hasValidCachedUser } = useUser();

      expect(hasValidCachedUser.value).toBe(false);
    });
  });
});
