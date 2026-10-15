import { mountSuspended } from "@nuxt/test-utils/runtime";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DefaultLayout from "~~/app/layouts/default.vue";
import { pairingKey, type FileNode } from "~~/app/lib/api";
import LibraryPage from "~~/app/pages/library/[...path].vue";
import { fakeServer } from "../support/fakeServer";

const file = (relativePath: string): FileNode => ({
  name: relativePath.split("/").pop()!,
  type: "file",
  relativePath,
});
const folder = (relativePath: string, children: FileNode[]): FileNode => ({
  name: relativePath.split("/").pop()!,
  type: "directory",
  relativePath,
  children,
});

const tree = folder("", [
  folder("TV", [
    file("TV/Pilot.mkv"),
    folder("TV/Show", [
      folder("TV/Show/Season 1", [
        file("TV/Show/Season 1/E01.mkv"),
        file("TV/Show/Season 1/E02.mkv"),
      ]),
    ]),
    folder("TV/Film (2020)", [file("TV/Film (2020)/Film.mkv")]),
  ]),
]);

// The library page relies on the default layout to provide favourites, tags and playback.
const InLayout = defineComponent(() => () => h(DefaultLayout, null, () => h(LibraryPage)));

const hrefs = async (route: string) => {
  const page = await mountSuspended(InLayout, { route });
  // The rescan action only shows once the folder's contents have loaded.
  await vi.waitFor(() => expect(page.text()).toContain("Look for new files"));
  return page.findAll("a").map((link) => link.attributes("href"));
};

describe("library folders", () => {
  beforeEach(() => {
    pairingKey.value = "key";
    fakeServer({
      "GET /api/library": () => Response.json(tree),
      "GET /api/favourites": () => Response.json([]),
      "GET /api/tags": () => Response.json({}),
      "GET /api/playback/session": () => Response.json({ session: null }),
    });
  });

  it("links a sub-folder shown inside a row by its full path", async () => {
    expect(await hrefs("/library/TV")).toContain("/library/TV/Show/Season%201");
  });

  it("opens a folder holding a single file straight at that file", async () => {
    expect(await hrefs("/library/TV")).toContain(
      `/detail/${encodeURIComponent("TV/Film (2020)/Film.mkv")}`,
    );
  });

  it("links each breadcrumb to its folder", async () => {
    expect(await hrefs("/library/TV/Show/Season%201")).toEqual(
      expect.arrayContaining([
        "/library",
        "/library/TV",
        "/library/TV/Show",
        "/library/TV/Show/Season%201",
      ]),
    );
  });
});
