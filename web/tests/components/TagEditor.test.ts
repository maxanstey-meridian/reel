import { mountSuspended } from "@nuxt/test-utils/runtime";
import { flushPromises } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import TagEditor from "~~/app/components/TagEditor.vue";
import { provideTags } from "~~/app/ports/tags";
import { deferred, fakeServer } from "../support/fakeServer";

// The default layout provides tags in the app; this stands in for it.
const WithTags = defineComponent(() => {
  provideTags(useTags());
  return () => h(TagEditor, { path: "a.mkv" });
});

describe("TagEditor", () => {
  it("can't edit until the saved tags have loaded", async () => {
    const load = deferred<Response>();
    fakeServer({ "GET /api/tags": () => load.promise });
    const editor = await mountSuspended(WithTags);

    expect(editor.get("button:not([aria-label])").attributes("disabled")).toBeDefined();
    load.resolve(Response.json({ "a.mkv": ["drama"] }));

    await vi.waitFor(() => expect(editor.text()).toContain("#drama"));
    expect(
      editor.get("button[aria-label='Delete tag drama']").attributes("disabled"),
    ).toBeUndefined();
  });

  it("stays read-only when the saved tags can't be loaded", async () => {
    fakeServer({
      "GET /api/tags": () =>
        Response.json({ code: "unavailable", message: "Down" }, { status: 503 }),
    });
    const editor = await mountSuspended(WithTags);

    await vi.waitFor(() => expect(editor.text()).toContain("#media"));
    await flushPromises();
    expect(
      editor.get("button[aria-label='Delete tag media']").attributes("disabled"),
    ).toBeDefined();
    expect(editor.get("button:not([aria-label])").attributes("disabled")).toBeDefined();
  });
});
