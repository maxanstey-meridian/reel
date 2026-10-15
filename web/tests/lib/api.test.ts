import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { client, pairingKey, ServerUnreachableError, thumbnailUrl, unwrap } from "~~/app/lib/api";
import { fakeServer, neverAnswers } from "../support/fakeServer";

// happy-dom runs AbortSignal.timeout on its own clock, out of reach of vi.useFakeTimers.
const useFakeClock = (): void => {
  vi.useFakeTimers();
  vi.spyOn(AbortSignal, "timeout").mockImplementation((ms) => {
    const controller = new AbortController();
    setTimeout(() => controller.abort(new DOMException("signal timed out", "TimeoutError")), ms);
    return controller.signal;
  });
};

describe("api client", () => {
  beforeEach(() => {
    pairingKey.value = "secret";
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("sends the pairing key with requests and file URLs", async () => {
    const server = fakeServer({ "GET /api/library": () => Response.json(null) });

    await client.GET("/api/library");

    expect(server.requests("GET /api/library")[0]?.headers.get("X-Secret-Key")).toBe("secret");
    expect(thumbnailUrl("a.mkv")).toBe("/api/files/thumbnail?path=a.mkv&token=secret");
  });

  it("reports network failures as an unreachable server", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Load failed")));

    await expect(client.GET("/api/library")).rejects.toBeInstanceOf(ServerUnreachableError);
  });

  it("gives up on a silent server after 15 seconds", async () => {
    useFakeClock();
    fakeServer({ "GET /api/playback/session": neverAnswers });

    let settled = false;

    const request = client.GET("/api/playback/session").finally(() => {
      settled = true;
    });
    const outcome = expect(request).rejects.toThrow("Can't reach the Reel server");
    await vi.advanceTimersByTimeAsync(14_999);
    expect(settled).toBe(false);
    await vi.advanceTimersByTimeAsync(1);
    await outcome;
  });

  it("lets a library rescan run for minutes", async () => {
    useFakeClock();
    fakeServer({ "POST /api/library/rescan": neverAnswers });
    let settled = false;

    const request = client.POST("/api/library/rescan").finally(() => {
      settled = true;
    });
    const outcome = expect(request).rejects.toBeInstanceOf(ServerUnreachableError);
    await vi.advanceTimersByTimeAsync(5 * 60_000);
    expect(settled).toBe(false);
    await vi.advanceTimersByTimeAsync(5 * 60_000);
    await outcome;
  });

  it("lets caller aborts propagate unchanged", async () => {
    fakeServer({ "GET /api/search": neverAnswers });
    const controller = new AbortController();

    const request = client.GET("/api/search", {
      params: { query: { q: "matrix" } },
      signal: controller.signal,
    });
    controller.abort();

    await expect(request).rejects.toMatchObject({ name: "AbortError" });
  });

  it("reports the server's own explanation of a refusal", async () => {
    fakeServer({
      "POST /api/library/rescan": () =>
        Response.json(
          { code: "rescan_refused", message: "The media folder is unavailable." },
          { status: 503 },
        ),
    });

    const failure = client.POST("/api/library/rescan").then(unwrap);

    await expect(failure).rejects.toThrow("The media folder is unavailable.");
  });

  it("forgets a pairing the server no longer accepts and returns to the connect page", async () => {
    fakeServer({
      "GET /api/library": () =>
        Response.json({ code: "unauthorized", message: "Not paired." }, { status: 401 }),
    });

    await client.GET("/api/library");

    expect(pairingKey.value).toBe("");
    await vi.waitFor(() => expect(useRoute().path).toBe("/connect"));
  });
});
