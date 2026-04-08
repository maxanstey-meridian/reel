// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);

const mockToastAdd = vi.fn();
vi.stubGlobal("useToast", () => ({ add: mockToastAdd }));

// Mock Rivet client
const mockFavouritesAll = vi.fn();
const mockFavouritesAdd = vi.fn();
const mockFavouritesRemove = vi.fn();

vi.mock("~~/generated/rivet/client/reel", () => ({
  favouritesAll: (...args: unknown[]) => mockFavouritesAll(...args),
  favouritesAdd: (...args: unknown[]) => mockFavouritesAdd(...args),
  favouritesRemove: (...args: unknown[]) => mockFavouritesRemove(...args),
}));

// Import after mocks
const { useFavourites } = await import("../useFavourites");

describe("useFavourites", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetch loads all favourites", async () => {
    const serverFavourites = [
      { relativePath: "Films/a.mp4", addedAt: "2026-01-01T00:00:00Z" },
      { relativePath: "Films/b.mp4", addedAt: "2026-01-02T00:00:00Z" },
    ];

    mockFavouritesAll.mockResolvedValue(serverFavourites);

    const { favourites, fetch } = useFavourites();
    await fetch();

    expect(favourites.value).toEqual(serverFavourites);
    expect(mockFavouritesAll).toHaveBeenCalled();
  });

  it("isFavourite returns correct boolean", async () => {
    mockFavouritesAll.mockResolvedValue([
      { relativePath: "Films/a.mp4", addedAt: "2026-01-01T00:00:00Z" },
    ]);

    const { isFavourite, fetch } = useFavourites();
    await fetch();

    expect(isFavourite("Films/a.mp4")).toBe(true);
    expect(isFavourite("Films/nonexistent.mp4")).toBe(false);
  });

  it("toggle adds favourite when not favourited", async () => {
    mockFavouritesAll.mockResolvedValue([]);
    mockFavouritesAdd.mockResolvedValue(undefined);

    const { favourites, toggle, fetch } = useFavourites();
    await fetch();

    expect(favourites.value).toHaveLength(0);

    await toggle("Films/new.mp4");

    // Optimistic update should have added it
    expect(favourites.value.some((f) => f.relativePath === "Films/new.mp4")).toBe(true);

    // POST was called via Rivet
    expect(mockFavouritesAdd).toHaveBeenCalledWith({ relativePath: "Films/new.mp4" });
  });

  it("toggle removes favourite when already favourited", async () => {
    mockFavouritesAll.mockResolvedValue([
      { relativePath: "Films/a.mp4", addedAt: "2026-01-01T00:00:00Z" },
    ]);
    mockFavouritesRemove.mockResolvedValue(undefined);

    const { favourites, toggle, fetch } = useFavourites();
    await fetch();

    expect(favourites.value).toHaveLength(1);

    await toggle("Films/a.mp4");

    // Optimistic update should have removed it
    expect(favourites.value.some((f) => f.relativePath === "Films/a.mp4")).toBe(false);

    // DELETE was called via generated Rivet client
    expect(mockFavouritesRemove).toHaveBeenCalledWith("Films/a.mp4");
  });

  it("shows toast after adding a favourite", async () => {
    mockFavouritesAll.mockResolvedValue([]);
    mockFavouritesAdd.mockResolvedValue(undefined);

    const { toggle, fetch } = useFavourites();
    await fetch();
    await toggle("Films/new.mp4");

    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Added to favourites",
        icon: "i-lucide-heart",
        color: "success",
      }),
    );
  });

  it("shows toast after removing a favourite", async () => {
    mockFavouritesAll.mockResolvedValue([
      { relativePath: "Films/a.mp4", addedAt: "2026-01-01T00:00:00Z" },
    ]);
    mockFavouritesRemove.mockResolvedValue(undefined);

    const { toggle, fetch } = useFavourites();
    await fetch();
    await toggle("Films/a.mp4");

    expect(mockToastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Removed from favourites",
        icon: "i-lucide-heart-off",
        color: "neutral",
      }),
    );
  });

  it("does not show toast when toggle fails", async () => {
    mockFavouritesAll.mockResolvedValue([]);
    mockFavouritesAdd.mockRejectedValue(new Error("Network error"));

    const { toggle, fetch } = useFavourites();
    await fetch();
    await toggle("Films/new.mp4");

    expect(mockToastAdd).not.toHaveBeenCalled();
  });
});
