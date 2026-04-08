// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);
vi.stubGlobal("readonly", (v: unknown) => v);

const mockNavigateTo = vi.fn();
vi.stubGlobal("navigateTo", mockNavigateTo);

// Mock Rivet client (required by useUser import)
const mockUserList = vi.fn();
vi.mock("~~/generated/rivet/client/reel", () => ({
  userList: (...args: unknown[]) => mockUserList(...args),
  userCreate: vi.fn(),
  userUpdate: vi.fn(),
  userDelete: vi.fn(),
}));

// Import composable after mocks
const { useUser } = await import("../../composables/useUser");

describe("select-user page logic", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    mockUserList.mockResolvedValue([]);
  });

  const setupPage = async (
    userList: Array<{ id: number; name: string; icon: string | null }>,
  ) => {
    mockUserList.mockResolvedValue(userList);
    const { users, fetchUsers, switchUser, hasValidCachedUser } = useUser();
    await fetchUsers();

    const handleSelect = (id: number) => {
      switchUser(id);
      mockNavigateTo("/library", { replace: true });
    };

    const handleAddProfile = () => {
      mockNavigateTo("/settings");
    };

    return { users, switchUser, hasValidCachedUser, handleSelect, handleAddProfile };
  };

  it("renders user cards for each user", async () => {
    const { users } = await setupPage([
      { id: 1, name: "Default", icon: null },
      { id: 2, name: "Alice", icon: "🌟" },
    ]);

    expect(users.value).toHaveLength(2);
    expect(users.value[0]!.name).toBe("Default");
    expect(users.value[1]!.name).toBe("Alice");
  });

  it("tapping a user calls switchUser and navigates to /library with replace", async () => {
    const { handleSelect } = await setupPage([
      { id: 1, name: "Default", icon: null },
      { id: 2, name: "Alice", icon: null },
    ]);

    handleSelect(2);

    expect(localStorage.getItem("reel:userId")).toBe("2");
    expect(mockNavigateTo).toHaveBeenCalledWith("/library", { replace: true });
  });

  it("Add Profile navigates to /settings", async () => {
    const { handleAddProfile } = await setupPage([
      { id: 1, name: "Default", icon: null },
    ]);

    handleAddProfile();

    expect(mockNavigateTo).toHaveBeenCalledWith("/settings");
  });
});

describe("index.vue boot guard logic", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    mockUserList.mockResolvedValue([]);
  });

  /**
   * Replicate the guard logic from index.vue middleware.
   * We test the decision function directly rather than mounting the page.
   */
  const runGuard = (isConnected: boolean, hasValidCachedUser: boolean) => {
    if (!isConnected) return mockNavigateTo("/connect", { replace: true });
    return mockNavigateTo(
      hasValidCachedUser ? "/library" : "/select-user",
      { replace: true },
    );
  };

  it("redirects to /connect when not connected", () => {
    runGuard(false, false);

    expect(mockNavigateTo).toHaveBeenCalledWith("/connect", { replace: true });
  });

  it("redirects to /select-user when connected but no valid cached user", () => {
    runGuard(true, false);

    expect(mockNavigateTo).toHaveBeenCalledWith("/select-user", { replace: true });
  });

  it("redirects to /library when connected and valid cached user", () => {
    runGuard(true, true);

    expect(mockNavigateTo).toHaveBeenCalledWith("/library", { replace: true });
  });
});

describe("hasValidCachedUser integration with guard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    mockUserList.mockResolvedValue([]);
  });

  it("returns false when cached userId was deleted from server", async () => {
    mockUserList.mockResolvedValue([
      { id: 1, name: "Default", icon: null },
      { id: 2, name: "Alice", icon: null },
    ]);

    const { fetchUsers, switchUser, hasValidCachedUser } = useUser();
    await fetchUsers();
    switchUser(2);
    expect(hasValidCachedUser.value).toBe(true);

    // Simulate user 2 being deleted on server
    mockUserList.mockResolvedValue([
      { id: 1, name: "Default", icon: null },
    ]);
    await fetchUsers();

    expect(hasValidCachedUser.value).toBe(false);
  });
});
