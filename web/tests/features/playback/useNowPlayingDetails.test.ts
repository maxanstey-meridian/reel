import { flushPromises } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { useNowPlayingDetails } from "~~/app/features/playback/useNowPlayingDetails";
import { fakeServer } from "../../support/fakeServer";

describe("useNowPlayingDetails", () => {
  it("asks for preview frames for video but not for audio", async () => {
    const server = fakeServer({
      "GET /api/files/subtitles": () => Response.json([]),
      "GET /api/files/trickplay": () => Response.json({}),
    });

    useNowPlayingDetails("Music/song.flac", null, undefined, false);
    await flushPromises();
    expect(server.requests("GET /api/files/trickplay")).toHaveLength(0);

    useNowPlayingDetails("Films/film.mkv", null, undefined, true);
    await flushPromises();
    expect(server.requests("GET /api/files/trickplay")).toHaveLength(1);
  });
});
