// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal(
  "useState",
  <T>(_key: string, init: () => T) => ref(init()),
);
vi.stubGlobal("computed", computed);

// Mock useConnection
const mockServerUrl = ref("http://localhost:8080");
const mockSecretKey = ref("test-key");

vi.mock("../useConnection", () => ({
  useConnection: () => ({
    serverUrl: mockServerUrl,
    secretKey: mockSecretKey,
  }),
}));

// Mock global fetch
const mockFetch = vi.fn();
vi.stubGlobal("fetch", mockFetch);

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

    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve(serverFavourites),
    });

    const { favourites, fetch } = useFavourites();
    await fetch();

    expect(favourites.value).toEqual(serverFavourites);
    expect(mockFetch).toHaveBeenCalledWith(
      "http://localhost:8080/api/favourites",
      expect.objectContaining({
        headers: expect.objectContaining({ "X-Secret-Key": "test-key" }),
      }),
    );
  });

  it("isFavourite returns correct boolean", async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: () =>
        Promise.resolve([
          { relativePath: "Films/a.mp4", addedAt: "2026-01-01T00:00:00Z" },
        ]),
    });

    const { isFavourite, fetch } = useFavourites();
    await fetch();

    expect(isFavourite("Films/a.mp4")).toBe(true);
    expect(isFavourite("Films/nonexistent.mp4")).toBe(false);
  });

  it("toggle adds favourite when not favourited", async () => {
    // Start with empty favourites
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve([]),
      })
      .mockResolvedValueOnce({ ok: true });

    const { favourites, toggle, fetch } = useFavourites();
    await fetch();

    expect(favourites.value).toHaveLength(0);

    await toggle("Films/new.mp4");

    // Optimistic update should have added it
    expect(favourites.value.some((f) => f.relativePath === "Films/new.mp4")).toBe(true);

    // POST was called
    expect(mockFetch).toHaveBeenLastCalledWith(
      "http://localhost:8080/api/favourites",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ relativePath: "Films/new.mp4" }),
      }),
    );
  });

  it("toggle removes favourite when already favourited", async () => {
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: () =>
          Promise.resolve([
            { relativePath: "Films/a.mp4", addedAt: "2026-01-01T00:00:00Z" },
          ]),
      })
      .mockResolvedValueOnce({ ok: true });

    const { favourites, toggle, fetch } = useFavourites();
    await fetch();

    expect(favourites.value).toHaveLength(1);

    await toggle("Films/a.mp4");

    // Optimistic update should have removed it
    expect(favourites.value.some((f) => f.relativePath === "Films/a.mp4")).toBe(false);

    // DELETE was called with query param
    expect(mockFetch).toHaveBeenLastCalledWith(
      "http://localhost:8080/api/favourites?path=Films%2Fa.mp4",
      expect.objectContaining({ method: "DELETE" }),
    );
  });
});
