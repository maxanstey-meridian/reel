import { mountSuspended } from "@nuxt/test-utils/runtime";
import { flushPromises } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import DefaultLayout from "~~/app/layouts/default.vue";
import { pairingKey, type FileNode, type PlaybackSession } from "~~/app/lib/api";
import NowPlayingPage from "~~/app/pages/now-playing.vue";
import { fakeServer, unreachable } from "../support/fakeServer";
import { fileNode, intent, observation } from "../support/fixtures";

// Now Playing relies on the default layout to provide playback, favourites and tags.
const InLayout = defineComponent(() => () => h(DefaultLayout, null, () => h(NowPlayingPage)));

const playing = (relativePath: string): PlaybackSession => ({
  sessionId: "6767fab3-64de-47de-ae53-b0e2981646c0",
  revision: 1,
  tvConnected: true,
  intent: intent({ relativePath, title: "Playing", desiredState: "playing" }),
  observation: observation({
    observationSequence: 1,
    appliedRevision: 1,
    state: "playing",
    relativePath,
    positionSeconds: 10,
    durationSeconds: 100,
    updatedAtUtc: "2026-08-11T00:00:00Z",
  }),
});

const library = (...files: { path: string; kind: "video" | "audio" }[]): FileNode =>
  fileNode({
    name: "",
    type: "directory",
    relativePath: "",
    children: files.map(({ path, kind }) =>
      fileNode({ name: path, type: "file", kind, relativePath: path }),
    ),
  });

const showNowPlaying = (
  session: PlaybackSession,
  tree: (request: Request) => Response | Promise<Response>,
) => {
  pairingKey.value = "key";
  const server = fakeServer({
    "GET /api/library": tree,
    "GET /api/favourites": () => Response.json([]),
    "GET /api/tags": () => Response.json({}),
    "GET /api/playback/session": () => Response.json({ session }),
    "GET /api/files/subtitles": () => Response.json([]),
    "GET /api/files/trickplay": () => Response.json({}),
  });
  return { server, mounted: mountSuspended(InLayout, { route: "/now-playing" }) };
};

describe("now playing previews", () => {
  beforeEach(() => {
    vi.useRealTimers();
  });

  it("never asks for preview frames for audio, even before the library loads", async () => {
    const { server, mounted } = showNowPlaying(playing("song.flac"), unreachable);
    const page = await mounted;

    await vi.waitFor(() => expect(page.text()).toContain("Playing on the TV"));
    await flushPromises();
    expect(server.requests("GET /api/files/trickplay")).toHaveLength(0);
  });

  it("asks for preview frames once the library shows the file is video", async () => {
    const { server, mounted } = showNowPlaying(playing("film.mkv"), () =>
      Response.json(library({ path: "film.mkv", kind: "video" })),
    );
    await mounted;

    await vi.waitFor(() => expect(server.requests("GET /api/files/trickplay")).toHaveLength(1));
  });
});

describe("now playing quality", () => {
  beforeEach(() => {
    vi.useRealTimers();
  });

  it("offers a quality only for video", async () => {
    const { mounted } = showNowPlaying(playing("song.flac"), () =>
      Response.json(library({ path: "song.flac", kind: "audio" })),
    );
    const page = await mounted;

    await vi.waitFor(() => expect(page.text()).toContain("Playing on the TV"));
    await flushPromises();
    expect(page.text()).not.toContain("Quality");
  });

  it("sends the picked quality and lists the subtitles a stream at it can carry", async () => {
    const session = playing("film.mkv");
    const { server, mounted } = showNowPlaying(session, () =>
      Response.json(library({ path: "film.mkv", kind: "video" })),
    );
    server.routes["PUT /api/playback/session/intent"] = () =>
      Response.json({
        ...session,
        revision: 2,
        intent: { ...session.intent, maxHeight: 720 },
      });
    const page = await mounted;
    await vi.waitFor(() => expect(page.text()).toContain("Quality"));

    const quality = page
      .findAllComponents({ name: "USelect" })
      .find((select) =>
        select.props("items")?.some((item: { label?: string }) => item.label === "720p"),
      );
    quality!.vm.$emit("update:modelValue", 720);

    await vi.waitFor(() =>
      expect(server.requests("PUT /api/playback/session/intent")).toHaveLength(1),
    );
    const body = await server.requests("PUT /api/playback/session/intent")[0]!.clone().json();
    expect(body).toMatchObject({ maxHeight: 720, subtitleIndex: null, desiredState: "playing" });
    await vi.waitFor(() =>
      expect(
        server
          .requests("GET /api/files/subtitles")
          .some((request) => new URL(request.url).searchParams.get("maxHeight") === "720"),
      ).toBe(true),
    );
  });
});
