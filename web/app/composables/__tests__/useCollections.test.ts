// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);

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

// Import after mocks
const { useCollections } = await import("../useCollections");

describe("useCollections", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetch loads all collections", async () => {
    const serverCollections = [
      { id: 1, name: "Watchlist", createdAt: "2026-01-01T00:00:00Z", itemCount: 3 },
      { id: 2, name: "Favourites", createdAt: "2026-01-02T00:00:00Z", itemCount: 0 },
    ];
    mockCollectionsList.mockResolvedValue(serverCollections);

    const { collections, fetch } = useCollections();
    await fetch();

    expect(collections.value).toEqual(serverCollections);
    expect(mockCollectionsList).toHaveBeenCalled();
  });

  it("fetch sets error on failure", async () => {
    mockCollectionsList.mockRejectedValue(new Error("Network error"));

    const { error, fetch } = useCollections();
    await fetch();

    expect(error.value).toBe("Network error");
  });

  it("create adds collection and returns it", async () => {
    const created = { id: 3, name: "New List", createdAt: "2026-01-03T00:00:00Z", itemCount: 0 };
    mockCollectionsCreate.mockResolvedValue(created);

    const { collections, create } = useCollections();
    const result = await create("New List");

    expect(result).toEqual(created);
    expect(collections.value).toContainEqual(created);
    expect(mockCollectionsCreate).toHaveBeenCalledWith({ name: "New List" });
  });

  it("create returns null and sets error on failure", async () => {
    mockCollectionsCreate.mockRejectedValue(new Error("Server error"));

    const { error, create } = useCollections();
    const result = await create("Bad List");

    expect(result).toBeNull();
    expect(error.value).toBe("Server error");
  });

  it("rename optimistically updates the collection name", async () => {
    mockCollectionsList.mockResolvedValue([
      { id: 1, name: "Old Name", createdAt: "2026-01-01T00:00:00Z", itemCount: 2 },
    ]);
    mockCollectionsRename.mockResolvedValue(undefined);

    const { collections, fetch, rename } = useCollections();
    await fetch();

    await rename(1, "New Name");

    expect(collections.value[0]!.name).toBe("New Name");
    expect(mockCollectionsRename).toHaveBeenCalledWith("1", { name: "New Name" });
  });

  it("rename reverts on failure", async () => {
    mockCollectionsList.mockResolvedValue([
      { id: 1, name: "Original", createdAt: "2026-01-01T00:00:00Z", itemCount: 0 },
    ]);
    mockCollectionsRename.mockRejectedValue(new Error("Failed"));

    const { collections, fetch, rename } = useCollections();
    await fetch();

    await rename(1, "Changed");

    expect(collections.value[0]!.name).toBe("Original");
  });

  it("remove optimistically removes collection", async () => {
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

  it("remove reverts on failure", async () => {
    mockCollectionsList.mockResolvedValue([
      { id: 1, name: "Keep Me", createdAt: "2026-01-01T00:00:00Z", itemCount: 0 },
    ]);
    mockCollectionsDelete.mockRejectedValue(new Error("Failed"));

    const { collections, fetch, remove } = useCollections();
    await fetch();

    await remove(1);

    expect(collections.value).toHaveLength(1);
    expect(collections.value[0]!.name).toBe("Keep Me");
  });

  it("addItem calls API and refetches to reconcile counts", async () => {
    // Initial fetch returns itemCount 2
    mockCollectionsList.mockResolvedValueOnce([
      { id: 1, name: "List", createdAt: "2026-01-01T00:00:00Z", itemCount: 2 },
    ]);
    mockCollectionsAddItem.mockResolvedValue(undefined);
    // Refetch after addItem returns the server's authoritative count
    mockCollectionsList.mockResolvedValueOnce([
      { id: 1, name: "List", createdAt: "2026-01-01T00:00:00Z", itemCount: 3 },
    ]);

    const { collections, fetch, addItem } = useCollections();
    await fetch();

    await addItem(1, "Films/movie.mp4");

    expect(collections.value[0]!.itemCount).toBe(3);
    expect(mockCollectionsAddItem).toHaveBeenCalledWith(1, { id: 1, path: "Films/movie.mp4" });
    // collectionsList called twice: initial fetch + post-add refetch
    expect(mockCollectionsList).toHaveBeenCalledTimes(2);
  });

  it("addItem duplicate does not inflate count — refetch reconciles", async () => {
    // Initial fetch: item already exists, count is 3
    mockCollectionsList.mockResolvedValueOnce([
      { id: 1, name: "List", createdAt: "2026-01-01T00:00:00Z", itemCount: 3 },
    ]);
    // Server silently ignores duplicate — no error
    mockCollectionsAddItem.mockResolvedValue(undefined);
    // Refetch returns the real count (still 3, not 4)
    mockCollectionsList.mockResolvedValueOnce([
      { id: 1, name: "List", createdAt: "2026-01-01T00:00:00Z", itemCount: 3 },
    ]);

    const { collections, fetch, addItem } = useCollections();
    await fetch();

    await addItem(1, "Films/existing.mp4");

    // Count should be 3 (reconciled from server), not 4 (optimistic drift)
    expect(collections.value[0]!.itemCount).toBe(3);
  });

  it("addItem reverts item count on failure", async () => {
    mockCollectionsList.mockResolvedValue([
      { id: 1, name: "List", createdAt: "2026-01-01T00:00:00Z", itemCount: 5 },
    ]);
    mockCollectionsAddItem.mockRejectedValue(new Error("Failed"));

    const { collections, fetch, addItem } = useCollections();
    await fetch();

    await addItem(1, "Films/movie.mp4");

    expect(collections.value[0]!.itemCount).toBe(5);
  });

  it("removeItem optimistically decrements item count", async () => {
    mockCollectionsList.mockResolvedValue([
      { id: 1, name: "List", createdAt: "2026-01-01T00:00:00Z", itemCount: 3 },
    ]);
    mockCollectionsRemoveItem.mockResolvedValue(undefined);

    const { collections, fetch, removeItem } = useCollections();
    await fetch();

    await removeItem(1, "Films/old.mp4");

    expect(collections.value[0]!.itemCount).toBe(2);
    expect(mockCollectionsRemoveItem).toHaveBeenCalledWith(1, "Films/old.mp4");
  });

  it("removeItem reverts item count on failure", async () => {
    mockCollectionsList.mockResolvedValue([
      { id: 1, name: "List", createdAt: "2026-01-01T00:00:00Z", itemCount: 3 },
    ]);
    mockCollectionsRemoveItem.mockRejectedValue(new Error("Failed"));

    const { collections, fetch, removeItem } = useCollections();
    await fetch();

    await removeItem(1, "Films/old.mp4");

    expect(collections.value[0]!.itemCount).toBe(3);
  });

  it("getWithItems returns collection with items", async () => {
    const fullCollection = {
      id: 1,
      name: "My List",
      createdAt: "2026-01-01T00:00:00Z",
      items: [
        { relativePath: "Films/a.mp4", position: 0, addedAt: "2026-01-01T00:00:00Z" },
        { relativePath: "Films/b.mp4", position: 1, addedAt: "2026-01-01T00:00:00Z" },
      ],
    };
    mockCollectionsGet.mockResolvedValue(fullCollection);

    const { getWithItems } = useCollections();
    const result = await getWithItems("1");

    expect(result).toEqual(fullCollection);
    expect(mockCollectionsGet).toHaveBeenCalledWith("1");
  });

  it("getWithItems returns null and sets error on failure", async () => {
    mockCollectionsGet.mockRejectedValue(new Error("Not found"));

    const { error, getWithItems } = useCollections();
    const result = await getWithItems("999");

    expect(result).toBeNull();
    expect(error.value).toBe("Not found");
  });
});
