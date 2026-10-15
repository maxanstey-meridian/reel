import { flushPromises } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import { deferred, fakeServer } from "../support/fakeServer";

const savesTags = async (request: Request) => {
  const { tags } = (await request.json()) as { tags: string[] };
  return Response.json(tags);
};

describe("useTags", () => {
  it("counts untagged items as #media, and is loaded only once the server answers", async () => {
    const load = deferred<Response>();
    fakeServer({ "GET /api/tags": () => load.promise });
    const { forPath, loaded } = useTags();
    await flushPromises();
    expect(loaded.value).toBe(false);

    load.resolve(Response.json({ "a.mkv": ["drama"] }));

    await vi.waitFor(() => expect(loaded.value).toBe(true));
    expect(forPath("a.mkv")).toEqual(["drama"]);
    expect(forPath("b.mkv")).toEqual(["media"]);
  });

  it("keeps tags that loaded while a save was in flight", async () => {
    const load = deferred<Response>();
    const server = fakeServer({ "GET /api/tags": () => load.promise, "PUT /api/tags": savesTags });
    const saving = deferred<null>();
    server.routes["PUT /api/tags"] = async (request) => {
      await saving.promise;
      return savesTags(request);
    };
    const { forPath, save } = useTags();

    const saved = save("a.mkv", ["comedy"]);
    load.resolve(Response.json({ "b.mkv": ["drama"] }));
    await vi.waitFor(() => expect(forPath("b.mkv")).toEqual(["drama"]));
    saving.resolve(null);
    await saved;

    expect(forPath("a.mkv")).toEqual(["comedy"]);
    expect(forPath("b.mkv")).toEqual(["drama"]);
  });

  it("keeps both of two saves that overlap", async () => {
    const server = fakeServer({ "GET /api/tags": () => Response.json({}) });
    const first = deferred<null>();
    server.routes["PUT /api/tags"] = async (request) => {
      const body = savesTags(request);
      if (server.requests("PUT /api/tags").length === 1) {
        await first.promise;
      }
      return body;
    };
    const { forPath, save } = useTags();
    await flushPromises();

    const slow = save("a.mkv", ["comedy"]);
    await save("b.mkv", ["drama"]);
    first.resolve(null);
    await slow;

    expect(forPath("a.mkv")).toEqual(["comedy"]);
    expect(forPath("b.mkv")).toEqual(["drama"]);
  });
});
