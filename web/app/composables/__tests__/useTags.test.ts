// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);

// Mock Rivet client
const mockTagsList = vi.fn();
const mockTagsRemove = vi.fn();
const mockTagsAll = vi.fn();

vi.mock("~~/generated/rivet/client/reel", () => ({
  tagsList: (...args: unknown[]) => mockTagsList(...args),
  tagsRemove: (...args: unknown[]) => mockTagsRemove(...args),
  tagsAll: (...args: unknown[]) => mockTagsAll(...args),
}));

// Mock rivetFetch for addTag (codegen gap — tagsAdd lacks path param)
const mockRivetFetch = vi.fn();

vi.mock("~~/generated/rivet/rivet", () => ({
  rivetFetch: (...args: unknown[]) => mockRivetFetch(...args),
}));

// Import after mocks
const { useTags } = await import("../useTags");

describe("useTags", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockTagsList.mockResolvedValue([]);
    mockTagsRemove.mockResolvedValue(undefined);
    mockTagsAll.mockResolvedValue([]);
    mockRivetFetch.mockResolvedValue(undefined);
  });

  it("getTags calls tagsList with path and populates tags ref", async () => {
    mockTagsList.mockResolvedValue(["action", "comedy"]);

    const { tags, getTags } = useTags();

    await getTags("Films/movie.mp4");

    expect(mockTagsList).toHaveBeenCalledWith("Films/movie.mp4");
    expect(tags.value).toEqual(["action", "comedy"]);
  });

  it("addTag calls rivetFetch with body and query, then re-fetches tags", async () => {
    mockTagsList.mockResolvedValueOnce(["action"]);

    const { tags, addTag } = useTags();

    await addTag("Films/movie.mp4", "action");

    expect(mockRivetFetch).toHaveBeenCalledWith(
      "POST",
      "/api/media/tags",
      { body: { tag: "action" }, query: { path: "Films/movie.mp4" } },
    );
    // Re-fetches after add
    expect(mockTagsList).toHaveBeenCalledWith("Films/movie.mp4");
    expect(tags.value).toEqual(["action"]);
  });

  it("removeTag calls tagsRemove with path and tag, then re-fetches tags", async () => {
    mockTagsList.mockResolvedValueOnce(["action", "comedy"]).mockResolvedValueOnce(["comedy"]);

    const { tags, getTags, removeTag } = useTags();
    await getTags("Films/movie.mp4");

    expect(tags.value).toEqual(["action", "comedy"]);

    await removeTag("Films/movie.mp4", "action");

    expect(mockTagsRemove).toHaveBeenCalledWith("Films/movie.mp4", "action");
    expect(tags.value).toEqual(["comedy"]);
  });

  it("getAllTags calls tagsAll and returns TagCount array", async () => {
    const tagCounts = [
      { tag: "action", count: 5 },
      { tag: "comedy", count: 3 },
    ];
    mockTagsAll.mockResolvedValue(tagCounts);

    const { getAllTags } = useTags();
    const result = await getAllTags();

    expect(mockTagsAll).toHaveBeenCalled();
    expect(result).toEqual(tagCounts);
  });

  it("loading is true during getTags and false after", async () => {
    let resolveApi: (value: string[]) => void;
    mockTagsList.mockReturnValue(
      new Promise<string[]>((resolve) => {
        resolveApi = resolve;
      }),
    );

    const { loading, getTags } = useTags();

    expect(loading.value).toBe(false);

    const promise = getTags("Films/movie.mp4");

    expect(loading.value).toBe(true);

    resolveApi!(["action"]);
    await promise;

    expect(loading.value).toBe(false);
  });

  it("loading resets to false even if API call fails", async () => {
    mockRivetFetch.mockRejectedValue(new Error("Network error"));

    const { loading, addTag } = useTags();

    await expect(addTag("Films/movie.mp4", "action")).rejects.toThrow("Network error");

    expect(loading.value).toBe(false);
  });
});
