import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createApp, h } from "vue";
import { usePlaybackSession } from "~~/app/features/playback/usePlaybackSession";
import type { PlaybackSession } from "~~/app/lib/api";
import { deferred, fakeServer } from "../../support/fakeServer";

const json = (body: object) => () => Response.json(body);
const pending = (promise: Promise<object>) => () => promise.then((body) => Response.json(body));

const activeSession: PlaybackSession = {
  sessionId: "6767fab3-64de-47de-ae53-b0e2981646c0",
  revision: 1,
  createdAtUtc: "2026-08-11T00:00:00Z",
  updatedAtUtc: "2026-08-11T00:00:00Z",
  tvConnected: true,
  intent: { relativePath: "Films/Test.mp4", title: "Test", desiredState: "playing" },
  observation: null,
};

const observation = (state: "playing" | "paused" | "ended") => ({
  observationSequence: 1,
  appliedRevision: activeSession.revision,
  state,
  relativePath: activeSession.intent.relativePath,
  positionSeconds: 10,
  durationSeconds: 100,
  updatedAtUtc: activeSession.updatedAtUtc,
});

const setHidden = (hidden: boolean): void => {
  Object.defineProperty(document, "visibilityState", {
    configurable: true,
    get: () => (hidden ? "hidden" : "visible"),
  });
  document.dispatchEvent(new Event("visibilitychange"));
};

let unmount: (() => void) | undefined;
let current: (() => ReturnType<typeof usePlaybackSession>) | undefined;

let server = fakeServer();
const answerPolls = (body: object | Promise<object>): void => {
  server.routes["GET /api/playback/session"] = body instanceof Promise ? pending(body) : json(body);
};
const answerCommands = (route: string, body: object | Promise<object>): void => {
  server.routes[route] = body instanceof Promise ? pending(body) : json(body);
};
const failCommands = (route: string, message: string): void => {
  server.routes[route] = () => Response.json({ code: "rejected", message }, { status: 409 });
};
const bodyOf = (route: string) => server.requests(route).at(-1)!.json();
const settle = () => vi.advanceTimersByTimeAsync(0);
const START = "POST /api/playback/session";
const INTENT = "PUT /api/playback/session/intent";
const POLL = "GET /api/playback/session";

const mountWithSession = async (session: PlaybackSession): Promise<void> => {
  answerPolls({ session });
  current = mountComposable();
  await vi.advanceTimersByTimeAsync(0);
};

const mountComposable = () => {
  let playback: ReturnType<typeof usePlaybackSession> | undefined;
  const root = document.createElement("div");
  const app = createApp({
    setup() {
      playback = usePlaybackSession();
      return () => h("div");
    },
  });
  app.mount(root);
  unmount = () => app.unmount();
  return () => playback!;
};

describe("usePlaybackSession", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    server = fakeServer();
    answerPolls({ session: null });
  });

  afterEach(() => {
    setHidden(false);
    unmount?.();
    unmount = undefined;
    vi.useRealTimers();
  });

  it("restores a server session on initial mount", async () => {
    answerPolls({ session: activeSession });
    const current = mountComposable();
    await settle();

    expect(current().session.value).toEqual(activeSession);
    expect(current().pending.value).toBe(true);
  });

  it("stores accepted intent without claiming it was observed", async () => {
    answerCommands(START, activeSession);
    const current = mountComposable();

    await current().start("Films/Test.mp4", 42);

    expect(await bodyOf(START)).toEqual({
      relativePath: "Films/Test.mp4",
      startPositionSeconds: 42,
    });
    expect(current().pending.value).toBe(true);
  });

  it("surfaces command failures and preserves the observed session", async () => {
    await mountWithSession(activeSession);
    failCommands(INTENT, "TV command rejected");

    expect(await current!().updateIntent("paused")).toBe(false);
    expect(current!().commandError.value).toBe("TV command rejected");
    expect(current!().session.value).toEqual(activeSession);
  });

  it("preserves an explicit null subtitle selection", async () => {
    await mountWithSession({
      ...activeSession,
      intent: { ...activeSession.intent, subtitleIndex: 2 },
    });
    answerCommands(INTENT, activeSession);

    await current!().updateIntent("playing", { subtitleIndex: null });

    expect(await bodyOf(INTENT)).toMatchObject({ subtitleIndex: null });
  });

  it("does not let an older poll overwrite a newer accepted command", async () => {
    const poll = deferred<object>();
    answerPolls(poll.promise);
    answerCommands(START, activeSession);
    const current = mountComposable();
    await settle();

    await current().start("Films/Test.mp4");
    poll.resolve({ session: null });
    await settle();

    expect(current().session.value).toEqual(activeSession);
  });

  it("ignores a poll answered before a control it raced", async () => {
    await mountWithSession(activeSession);
    const poll = deferred<object>();
    answerPolls(poll.promise);
    await vi.advanceTimersByTimeAsync(1_000);
    const paused = {
      ...activeSession,
      revision: 2,
      intent: { ...activeSession.intent, desiredState: "paused" },
    };
    answerCommands(INTENT, paused);

    await current!().updateIntent("paused");
    poll.resolve({ session: activeSession });
    await settle();

    expect(current!().session.value).toEqual(paused);
  });

  it("accepts a poll carrying a newer TV observation for the same revision", async () => {
    await mountWithSession(activeSession);
    const observed = { ...activeSession, observation: observation("playing") };
    answerPolls({ session: observed });

    await vi.advanceTimersByTimeAsync(1_000);

    expect(current!().session.value).toEqual(observed);
  });

  it("unlocks controls once the server accepts a command, even before the TV applies it", async () => {
    await mountWithSession(activeSession);
    const command = deferred<object>();
    answerCommands(INTENT, command.promise);

    const result = current!().updateIntent("stopped");
    expect(current!().sending.value).toBe(true);
    command.resolve({
      ...activeSession,
      revision: 2,
      intent: { ...activeSession.intent, desiredState: "stopped" },
    });
    await result;

    expect(current!().sending.value).toBe(false);
    expect(current!().pending.value).toBe(true);
  });

  it("ignores a second command while one is in flight", async () => {
    const first = deferred<object>();
    answerCommands(START, first.promise);
    const current = mountComposable();

    const firstResult = current().start("Films/First.mp4");
    await current().start("Films/Second.mp4");
    first.resolve(activeSession);
    await firstResult;

    expect(server.requests(START)).toHaveLength(1);
  });

  it("stops polling while the page is hidden and refreshes on return", async () => {
    await mountWithSession(activeSession);
    const before = server.requests(POLL).length;

    setHidden(true);
    await vi.advanceTimersByTimeAsync(60_000);
    expect(server.requests(POLL)).toHaveLength(before);

    setHidden(false);
    await settle();
    expect(server.requests(POLL)).toHaveLength(before + 1);
  });

  it("retains the finished title after the live session expires", async () => {
    answerPolls({ session: { ...activeSession, observation: observation("ended") } });
    const current = mountComposable();
    await settle();
    const finished = { relativePath: activeSession.intent.relativePath, title: "Test" };
    expect(current().completed.value).toEqual(finished);
    answerPolls({ session: null });
    await vi.advanceTimersByTimeAsync(1_000);
    expect(current().session.value).toBeNull();
    expect(current().completed.value).toEqual(finished);
  });
});
