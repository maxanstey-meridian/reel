import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeServer } from "../support/fakeServer";

const found = [{ name: "New", type: "file", relativePath: "new.mkv" }];

const searchServer = () =>
  fakeServer({
    "GET /api/search": () => Response.json(found),
    "POST /api/library/rescan": () => Response.json({ mediaCount: 1 }),
  });

let scope = effectScope();
const mountSearch = () => {
  scope = effectScope();
  return scope.run(useSearch)!;
};

describe("useSearch", () => {
  afterEach(() => scope.stop());
  it("searches once for the settled query and stays searching until then", async () => {
    const server = searchServer();
    vi.useFakeTimers();
    const { query, searching, results } = mountSearch();
    query.value = "first";
    await vi.advanceTimersByTimeAsync(100);
    query.value = "second ";
    expect(searching.value).toBe(true);
    await vi.advanceTimersByTimeAsync(300);
    vi.useRealTimers();

    await vi.waitFor(() => expect(results.value).toEqual(found));
    expect(searching.value).toBe(false);
    const searches = server.requests("GET /api/search");
    expect(searches).toHaveLength(1);
    expect(new URL(searches[0]!.url).searchParams.get("q")).toBe("second");
  });

  it("rescans the index before repeating the search", async () => {
    const server = searchServer();
    const order: string[] = [];
    server.routes["POST /api/library/rescan"] = () => {
      order.push("rescan");
      return Response.json({ mediaCount: 1 });
    };
    server.routes["GET /api/search"] = () => {
      order.push("search");
      return Response.json(found);
    };
    vi.useFakeTimers();
    const { query, results, rescan } = mountSearch();
    query.value = "new";
    await vi.advanceTimersByTimeAsync(300);
    vi.useRealTimers();
    await vi.waitFor(() => expect(results.value).toEqual(found));

    await rescan();

    expect(order).toEqual(["search", "rescan", "search"]);
  });
});
