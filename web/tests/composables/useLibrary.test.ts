import { describe, expect, it, vi } from "vitest";
import { readBrowseCache, writeBrowseCache } from "~~/app/lib/browse-cache";
import { deferred, fakeServer, unreachable } from "../support/fakeServer";
import { fileNode } from "../support/fixtures";

const saved = fileNode({
  name: "",
  type: "directory",
  relativePath: "",
  children: [fileNode({ name: "Old.mkv", type: "file", relativePath: "Old.mkv" })],
});
const fresh = fileNode({ name: "", type: "directory", relativePath: "", children: [] });

const libraryServer = () =>
  fakeServer({
    "GET /api/library": () => Response.json(fresh),
    "POST /api/library/rescan": () => Response.json({ mediaCount: 0 }),
  });

const build = useRuntimeConfig().app.buildId;

describe("useLibrary", () => {
  it("shows the saved library while refreshing, then replaces and saves it", async () => {
    const server = libraryServer();
    writeBrowseCache(build, "library", saved);
    const loaded = deferred<Response>();
    server.routes["GET /api/library"] = () => loaded.promise;
    const { tree, loading } = useLibrary();

    expect(tree.value).toEqual(saved);
    expect(loading.value).toBe(false);
    loaded.resolve(Response.json(fresh));
    await vi.waitFor(() => expect(tree.value).toEqual(fresh));
    await vi.waitFor(() => expect(readBrowseCache(build, "library")).toEqual(fresh));
  });

  it("keeps showing the saved library when the server is unreachable", async () => {
    fakeServer({ "GET /api/library": unreachable });
    writeBrowseCache(build, "library", saved);
    const { tree, error } = useLibrary();

    await vi.waitFor(() => expect(error.value?.message).toBe("Can't reach the Reel server"));
    expect(tree.value).toEqual(saved);
  });

  it("shares one fetched tree between pages", async () => {
    const server = libraryServer();
    const first = useLibrary();
    await vi.waitFor(() => expect(first.tree.value).toEqual(fresh));
    expect(useLibrary().tree.value).toEqual(fresh);
    expect(server.requests("GET /api/library")).toHaveLength(1);
  });

  it("rescans before fetching the new tree and ignores a late response from before it", async () => {
    const server = libraryServer();
    const initial = deferred<Response>();
    server.routes["GET /api/library"] = () => initial.promise;
    const { tree, rescan } = useLibrary();
    server.routes["GET /api/library"] = () => Response.json(fresh);

    await rescan();
    initial.resolve(Response.json(saved));
    await initial.promise;
    await nextTick();

    expect(server.requests("GET /api/library")).toHaveLength(2);
    expect(tree.value).toEqual(fresh);
  });

  it("tells the user when a rescan is refused", async () => {
    const server = libraryServer();
    server.routes["POST /api/library/rescan"] = () =>
      Response.json(
        { code: "rescan_refused", message: "The media folder is unavailable." },
        { status: 503 },
      );
    const { tree, rescan } = useLibrary();
    await vi.waitFor(() => expect(tree.value).toEqual(fresh));

    await rescan();

    expect(useToast().toasts.value).toContainEqual(
      expect.objectContaining({ description: "The media folder is unavailable." }),
    );
    expect(tree.value).toEqual(fresh);
  });
});
