import { ref } from "vue";
import {
  collectionsList,
  collectionsCreate,
  collectionsGet,
  collectionsRename,
  collectionsDelete,
  collectionsAddItem,
  collectionsRemoveItem,
} from "~~/generated/rivet/client/reel";
import type { Collection, CollectionWithItems } from "~~/generated/rivet/types/domain";

export type { Collection, CollectionWithItems };

export const useCollections = () => {
  const collections = useState<Collection[]>("reel:collections", () => []);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const fetch = async () => {
    loading.value = true;
    error.value = null;

    try {
      collections.value = await collectionsList();
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Failed to fetch collections";
    } finally {
      loading.value = false;
    }
  };

  const create = async (name: string) => {
    error.value = null;

    try {
      const created = await collectionsCreate({ name });
      collections.value = [created, ...collections.value];
      return created;
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Failed to create collection";
      return null;
    }
  };

  const rename = async (id: number, name: string) => {
    error.value = null;
    const prev = collections.value;

    // Optimistic update
    collections.value = collections.value.map((c) =>
      c.id === id ? { ...c, name } : c,
    );

    try {
      await collectionsRename(String(id), { name });
    } catch (e) {
      collections.value = prev;
      error.value = e instanceof Error ? e.message : "Failed to rename collection";
    }
  };

  const remove = async (id: number) => {
    error.value = null;
    const prev = collections.value;

    // Optimistic update
    collections.value = collections.value.filter((c) => c.id !== id);

    try {
      await collectionsDelete(String(id));
    } catch (e) {
      collections.value = prev;
      error.value = e instanceof Error ? e.message : "Failed to delete collection";
    }
  };

  const addItem = async (collectionId: number, path: string) => {
    error.value = null;

    // Optimistic: bump item count
    collections.value = collections.value.map((c) =>
      c.id === collectionId ? { ...c, itemCount: c.itemCount + 1 } : c,
    );

    try {
      await collectionsAddItem(collectionId, { id: collectionId, path });
      // Refetch to reconcile counts — server is idempotent on duplicates,
      // so the optimistic +1 may overcount if the item already existed.
      collections.value = await collectionsList();
    } catch (e) {
      // Revert
      collections.value = collections.value.map((c) =>
        c.id === collectionId ? { ...c, itemCount: c.itemCount - 1 } : c,
      );
      error.value = e instanceof Error ? e.message : "Failed to add item";
    }
  };

  const removeItem = async (collectionId: number, path: string) => {
    error.value = null;

    // Optimistic: decrement item count
    collections.value = collections.value.map((c) =>
      c.id === collectionId ? { ...c, itemCount: Math.max(0, c.itemCount - 1) } : c,
    );

    try {
      await collectionsRemoveItem(collectionId, path);
    } catch (e) {
      // Revert
      collections.value = collections.value.map((c) =>
        c.id === collectionId ? { ...c, itemCount: c.itemCount + 1 } : c,
      );
      error.value = e instanceof Error ? e.message : "Failed to remove item";
    }
  };

  const getWithItems = async (id: string) => {
    try {
      return await collectionsGet(id);
    } catch (e) {
      error.value = e instanceof Error ? e.message : "Failed to fetch collection";
      return null;
    }
  };

  return { collections, loading, error, fetch, create, rename, remove, addItem, removeItem, getWithItems };
};
