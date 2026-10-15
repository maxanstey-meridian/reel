import { flushPromises } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import { readBrowseCache } from "~~/app/lib/browse-cache";
import { deferred, fakeServer, unreachable } from "../support/fakeServer";

const saved = { relativePath: "Films/a.mp4", addedAt: "2026-01-01T00:00:00Z" };

/** Behaves like the real favourites store: adds and removes change what the next load returns. */
const favouritesServer = () => {
  let stored = [saved];
  return fakeServer({
    "GET /api/favourites": () => Response.json(stored),
    "POST /api/favourites": async (request) => {
      const { relativePath } = (await request.json()) as { relativePath: string };
      stored = [{ relativePath, addedAt: "2026-02-01T00:00:00Z" }, ...stored];
      return new Response(null, { status: 204 });
    },
    "DELETE /api/favourites": (request) => {
      const path = new URL(request.url).searchParams.get("path");
      stored = stored.filter((favourite) => favourite.relativePath !== path);
      return new Response(null, { status: 204 });
    },
  });
};

const loadFavourites = async () => {
  const favourites = useFavourites();
  await vi.waitFor(() => expect(favourites.loading.value).toBe(false));
  return favourites;
};

const build = useRuntimeConfig().app.buildId;

describe("useFavourites", () => {
  it("loads favourites from the server", async () => {
    favouritesServer();
    const { isFavourite } = await loadFavourites();
    expect(isFavourite("Films/a.mp4")).toBe(true);
    expect(isFavourite("Films/other.mp4")).toBe(false);
  });

  it("does not save the empty list shown when loading fails", async () => {
    fakeServer({ "GET /api/favourites": unreachable });
    const { error } = useFavourites();

    await vi.waitFor(() => expect(error.value).toBeTruthy());
    await flushPromises();

    expect(readBrowseCache(build, "favourites")).toBeNull();
  });

  it("adds and removes a favourite and saves the result", async () => {
    favouritesServer();
    const { isFavourite, toggle } = await loadFavourites();

    await toggle("Films/new.mp4");
    expect(isFavourite("Films/new.mp4")).toBe(true);

    await toggle("Films/a.mp4");
    expect(isFavourite("Films/a.mp4")).toBe(false);
    await vi.waitFor(() =>
      expect(readBrowseCache(build, "favourites")).toEqual([
        expect.objectContaining({ relativePath: "Films/new.mp4" }),
      ]),
    );
  });

  it("restores the heart when the server rejects the change", async () => {
    const server = favouritesServer();
    server.routes["DELETE /api/favourites"] = () =>
      Response.json({ code: "server_error", message: "Database locked" }, { status: 500 });
    const { isFavourite, toggle } = await loadFavourites();

    await toggle("Films/a.mp4");

    expect(isFavourite("Films/a.mp4")).toBe(true);
  });

  it("ignores a second tap while the first change is still being saved", async () => {
    const server = favouritesServer();
    const saving = deferred<Response>();
    server.routes["POST /api/favourites"] = () => saving.promise;
    const { isFavourite, toggle } = await loadFavourites();

    const first = toggle("Films/new.mp4");
    await toggle("Films/new.mp4");
    saving.resolve(new Response(null, { status: 204 }));
    await first;

    expect(server.requests("POST /api/favourites")).toHaveLength(1);
    expect(server.requests("DELETE /api/favourites")).toHaveLength(0);
    expect(isFavourite("Films/new.mp4")).toBe(true);
  });

  describe("a load that started before a change", () => {
    const withStaleFirstLoad = () => {
      const server = favouritesServer();
      const load = server.routes["GET /api/favourites"]!;
      const staleLoad = deferred<Response>();
      server.routes["GET /api/favourites"] = () => {
        server.routes["GET /api/favourites"] = load;
        return staleLoad.promise;
      };
      return { server, staleLoad };
    };

    it("does not undo the change when it answers after the change is saved", async () => {
      const { staleLoad } = withStaleFirstLoad();
      const { isFavourite, toggle } = useFavourites();

      await toggle("Films/new.mp4");
      staleLoad.resolve(Response.json([saved]));
      await flushPromises();

      expect(isFavourite("Films/new.mp4")).toBe(true);
    });

    it("is replaced by a fresh load when it answers while the change is being saved", async () => {
      const { server, staleLoad } = withStaleFirstLoad();
      const store = server.routes["POST /api/favourites"]!;
      const saving = deferred<null>();
      server.routes["POST /api/favourites"] = async (request) => {
        await saving.promise;
        return store(request);
      };
      const { isFavourite, toggle } = useFavourites();

      const adding = toggle("Films/new.mp4");
      staleLoad.resolve(Response.json([saved]));
      await vi.waitFor(() => expect(isFavourite("Films/new.mp4")).toBe(false));
      saving.resolve(null);
      await adding;

      await vi.waitFor(() => expect(isFavourite("Films/new.mp4")).toBe(true));
    });
  });
});
