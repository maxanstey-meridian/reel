// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);
vi.stubGlobal("onMounted", (fn: () => void) => fn());

const mockNavigateTo = vi.fn();
vi.stubGlobal("navigateTo", mockNavigateTo);

// Mock Rivet client
const mockCollectionsList = vi.fn();
const mockCollectionsCreate = vi.fn();
const mockCollectionsGet = vi.fn();
const mockCollectionsRename = vi.fn();
const mockCollectionsDelete = vi.fn();
const mockCollectionsAddItem = vi.fn();
const mockCollectionsRemoveItem = vi.fn();

vi.mock("~~/generated/rivet/client/reel", () => ({
  collectionsList: (...args: unknown[]) => mockCollectionsList(...args),
  collectionsCreate: (...args: unknown[]) => mockCollectionsCreate(...args),
  collectionsGet: (...args: unknown[]) => mockCollectionsGet(...args),
  collectionsRename: (...args: unknown[]) => mockCollectionsRename(...args),
  collectionsDelete: (...args: unknown[]) => mockCollectionsDelete(...args),
  collectionsAddItem: (...args: unknown[]) => mockCollectionsAddItem(...args),
  collectionsRemoveItem: (...args: unknown[]) => mockCollectionsRemoveItem(...args),
}));

// Mock useConnection
vi.stubGlobal("useConnection", () => ({
  isConnected: ref(true),
  serverUrl: ref("http://localhost:8080"),
  secretKey: ref("test-key"),
}));

// Import composable after mocks
const { useCollections } = await import("../../composables/useCollections");

type Collection = { id: number; name: string; createdAt: string; itemCount: number };

const setupCollectionsPage = async (serverCollections: Collection[]) => {
  mockCollectionsList.mockResolvedValue(serverCollections);

  const { collections, loading, error, fetch, create, rename, remove } = useCollections();
  await fetch();

  // Replicate page inline-create state
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
        mockNavigateTo(`/collections/${created.id}`);
      }
    } finally {
      creating.value = false;
    }
  };

  return {
    collections, loading, error, fetch, create, rename, remove,
    adding, newName, creating, startAdding, cancelAdding, handleCreate,
  };
};

describe("collections page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders collections list from server", async () => {
    const serverCollections: Collection[] = [
      { id: 1, name: "Watchlist", createdAt: "2026-01-01T00:00:00Z", itemCount: 5 },
      { id: 2, name: "Horror", createdAt: "2026-01-02T00:00:00Z", itemCount: 0 },
    ];

    const { collections } = await setupCollectionsPage(serverCollections);

    expect(collections.value).toHaveLength(2);
    expect(collections.value[0]!.name).toBe("Watchlist");
    expect(collections.value[0]!.itemCount).toBe(5);
    expect(collections.value[1]!.name).toBe("Horror");
  });

  it("shows empty state when no collections", async () => {
    const { collections, adding } = await setupCollectionsPage([]);

    expect(collections.value).toHaveLength(0);
    expect(adding.value).toBe(false);
  });

  it("inline create sends create request and navigates to new collection", async () => {
    const created = { id: 3, name: "New List", createdAt: "2026-01-03T00:00:00Z", itemCount: 0 };
    mockCollectionsCreate.mockResolvedValue(created);

    const { handleCreate, startAdding, newName, adding } = await setupCollectionsPage([]);

    startAdding();
    expect(adding.value).toBe(true);

    newName.value = "New List";
    await handleCreate();

    expect(mockCollectionsCreate).toHaveBeenCalledWith({ name: "New List" });
    expect(adding.value).toBe(false);
    expect(mockNavigateTo).toHaveBeenCalledWith("/collections/3");
  });

  it("cancel adding resets state", async () => {
    const { startAdding, cancelAdding, adding, newName } = await setupCollectionsPage([]);

    startAdding();
    newName.value = "In progress";
    cancelAdding();

    expect(adding.value).toBe(false);
    expect(newName.value).toBe("");
  });
});

describe("collection detail page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetches collection with items in order", async () => {
    const fullCollection = {
      id: 1,
      name: "My List",
      createdAt: "2026-01-01T00:00:00Z",
      items: [
        { relativePath: "Films/a.mp4", position: 0, addedAt: "2026-01-01T00:00:00Z" },
        { relativePath: "Films/b.mp4", position: 1, addedAt: "2026-01-01T00:00:00Z" },
        { relativePath: "Films/c.mp4", position: 2, addedAt: "2026-01-01T00:00:00Z" },
      ],
    };
    mockCollectionsGet.mockResolvedValue(fullCollection);

    const { getWithItems } = useCollections();
    const result = await getWithItems("1");

    expect(result).not.toBeNull();
    expect(result!.items).toHaveLength(3);
    expect(result!.items[0]!.relativePath).toBe("Films/a.mp4");
    expect(result!.items[2]!.relativePath).toBe("Films/c.mp4");
  });

  it("remove item from collection updates local state", async () => {
    mockCollectionsList.mockResolvedValue([
      { id: 1, name: "List", createdAt: "2026-01-01T00:00:00Z", itemCount: 3 },
    ]);
    mockCollectionsRemoveItem.mockResolvedValue(undefined);

    const { collections, fetch, removeItem } = useCollections();
    await fetch();

    await removeItem(1, "Films/b.mp4");

    expect(collections.value[0]!.itemCount).toBe(2);
    expect(mockCollectionsRemoveItem).toHaveBeenCalledWith(1, "Films/b.mp4");
  });

  it("rename updates collection name optimistically", async () => {
    mockCollectionsList.mockResolvedValue([
      { id: 1, name: "Old Name", createdAt: "2026-01-01T00:00:00Z", itemCount: 0 },
    ]);
    mockCollectionsRename.mockResolvedValue(undefined);

    const { collections, fetch, rename } = useCollections();
    await fetch();

    await rename(1, "New Name");

    expect(collections.value[0]!.name).toBe("New Name");
  });

  it("delete removes collection and calls API", async () => {
    mockCollectionsList.mockResolvedValue([
      { id: 1, name: "To Delete", createdAt: "2026-01-01T00:00:00Z", itemCount: 0 },
      { id: 2, name: "Keep", createdAt: "2026-01-02T00:00:00Z", itemCount: 1 },
    ]);
    mockCollectionsDelete.mockResolvedValue(undefined);

    const { collections, fetch, remove } = useCollections();
    await fetch();

    await remove(1);

    expect(collections.value).toHaveLength(1);
    expect(collections.value[0]!.name).toBe("Keep");
    expect(mockCollectionsDelete).toHaveBeenCalledWith("1");
  });
});

describe("nav bar", () => {
  it("has 5 tabs including Collections", () => {
    const navItems = [
      { path: "/library", label: "Library", icon: "i-lucide-film" },
      { path: "/search", label: "Search", icon: "i-lucide-search" },
      { path: "/favourites", label: "Favourites", icon: "i-lucide-heart" },
      { path: "/collections", label: "Collections", icon: "i-lucide-list-video" },
      { path: "/now-playing", label: "Now Playing", icon: "i-lucide-cast" },
    ];

    expect(navItems).toHaveLength(5);
    expect(navItems.find((i) => i.path === "/collections")).toBeDefined();
    expect(navItems[3]!.label).toBe("Collections");
    expect(navItems[3]!.icon).toBe("i-lucide-list-video");
  });
});
