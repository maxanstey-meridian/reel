import { expect, test, type APIRequestContext } from "@playwright/test";
import { writeFile, unlink } from "node:fs/promises";
import { resolve } from "node:path";

const serverUrl = `http://127.0.0.1:${process.env.E2E_SERVER_PORT ?? "18080"}`;
const secretKey = "REEL-E2E";
const fixturePath = "Browser E2E Fixture.mp4";
const connectUrl = `/connect?key=${secretKey}`;

type Session = {
  sessionId: string;
  revision: number;
  intent: {
    relativePath: string;
    desiredState: "playing" | "paused" | "stopped";
    pendingSeek: { seekId: number; positionSeconds: number } | null;
  };
};

const getSession = async (request: APIRequestContext): Promise<Session> => {
  const response = await request.get(`${serverUrl}/api/playback/session`, {
    headers: { "X-Secret-Key": secretKey },
  });
  expect(response.ok()).toBe(true);
  const body = (await response.json()) as { session: Session | null };
  expect(body.session).not.toBeNull();
  return body.session!;
};

const reportObservation = async (
  request: APIRequestContext,
  session: Session,
  state: "playing" | "paused" | "idle" | "ended",
  position = 12,
): Promise<void> => {
  const current = await request.get(`${serverUrl}/api/playback/session`, {
    headers: { "X-Secret-Key": secretKey },
  });
  const currentBody = (await current.json()) as {
    session: { observation?: { observationSequence: number } | null } | null;
  };
  const response = await request.put(`${serverUrl}/api/playback/session/observation`, {
    headers: { "X-Secret-Key": secretKey },
    data: {
      sessionId: session.sessionId,
      observationSequence: (currentBody.session?.observation?.observationSequence ?? 0) + 1,
      appliedRevision: session.revision,
      appliedSeekId: session.intent.pendingSeek?.seekId ?? null,
      state,
      relativePath: state === "idle" ? null : session.intent.relativePath,
      positionSeconds: state === "idle" ? 0 : position,
      durationSeconds: state === "idle" ? 0 : 120,
      subtitleIndex: null,
      errorCode: null,
      errorMessage: null,
    },
  });
  expect(response.ok()).toBe(true);
};

test.describe("Server-backed browser playback contract", () => {
  test("web controls reflect a simulated TV observation", async ({ page, request }) => {
    // The fixture database survives separate runs; this journey starts from zero.
    await request.post(`${serverUrl}/api/progress`, {
      headers: { "X-Secret-Key": secretKey },
      data: { relativePath: fixturePath, positionSeconds: 0, durationSeconds: 120 },
    });
    await page.goto(connectUrl);
    await expect(page).toHaveURL(/\/library$/);

    await page.goto("/search");
    await page.getByPlaceholder("Search titles or #tags...").fill("Browser E2E Fixture");
    await page.getByText("Browser E2E Fixture", { exact: true }).click();
    await expect(page.getByRole("heading", { name: "Browser E2E Fixture" })).toBeVisible();

    await page.getByRole("button", { name: "Play on TV", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Now Playing" })).toBeVisible();
    const nowPlaying = page.locator("main");
    await expect(nowPlaying.getByText("TV disconnected", { exact: true })).toBeVisible();

    let session = await getSession(request);
    expect(session.intent.relativePath).toBe(fixturePath);
    expect(session.intent.desiredState).toBe("playing");
    await reportObservation(request, session, "playing");
    await expect(nowPlaying.getByText("Playing on the TV", { exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Pause playback" }).click();
    await expect(nowPlaying.getByText("Waiting for the TV…", { exact: true })).toBeVisible();
    await expect.poll(async () => (await getSession(request)).intent.desiredState).toBe("paused");
    session = await getSession(request);
    await reportObservation(request, session, "paused");
    await expect(nowPlaying.getByText("Paused", { exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Forward 10 seconds" }).click();
    await expect
      .poll(async () => (await getSession(request)).intent.pendingSeek?.positionSeconds)
      .toBe(22);
    session = await getSession(request);
    expect(session.intent.desiredState).toBe("paused");
    await reportObservation(request, session, "paused", 22);
    await expect(page.getByLabel("Playback position")).toHaveValue("22");
    await expect(page.getByRole("button", { name: "Forward 10 seconds" })).toBeEnabled();

    const scrubber = page.getByLabel("Playback position");
    const revision = session.revision;
    await scrubber.evaluate((element) => {
      (element as HTMLInputElement).value = "45";
      element.dispatchEvent(new Event("input", { bubbles: true }));
    });
    expect((await getSession(request)).revision).toBe(revision);
    await scrubber.dispatchEvent("pointercancel");
    await scrubber.dispatchEvent("change");
    expect((await getSession(request)).revision).toBe(revision);
    await scrubber.evaluate((element) => {
      (element as HTMLInputElement).value = "45";
      element.dispatchEvent(new Event("input", { bubbles: true }));
      element.dispatchEvent(new Event("change", { bubbles: true }));
    });
    await expect
      .poll(async () => (await getSession(request)).intent.pendingSeek?.positionSeconds)
      .toBe(45);
    session = await getSession(request);
    await reportObservation(request, session, "paused", 45);
    await expect(scrubber).toHaveValue("45");
    await expect(page.getByRole("button", { name: "Stop playback" })).toBeEnabled();

    await page.getByRole("button", { name: "Stop playback" }).click();
    await expect(nowPlaying.getByText("Waiting for the TV…", { exact: true })).toBeVisible();
    await expect.poll(async () => (await getSession(request)).intent.desiredState).toBe("stopped");
    session = await getSession(request);
    await reportObservation(request, session, "idle");
    await expect(nowPlaying.getByText("Stopped", { exact: true })).toBeVisible();
    await expect(nowPlaying.getByText("Playing on the TV", { exact: true })).not.toBeVisible();
  });
});

test("saved progress offers resume after reload and pull-to-refresh reloads the library", async ({
  page,
  request,
}) => {
  const response = await request.post(`${serverUrl}/api/progress`, {
    headers: { "X-Secret-Key": secretKey },
    data: { relativePath: fixturePath, positionSeconds: 40, durationSeconds: 120 },
  });
  expect(response.ok()).toBe(true);
  await page.goto(connectUrl);
  await expect(page).toHaveURL(/\/library$/);
  const main = page.locator("main");
  await expect(main.getByText("Browser E2E Fixture", { exact: true })).toBeVisible();
  const reload = page.waitForResponse((r) => new URL(r.url()).pathname === "/api/library");
  // Library refreshes by pulling down from the top of the list.
  const touch = (clientY: number) => ({ touches: [{ identifier: 0, clientX: 195, clientY }] });
  await main.dispatchEvent("touchstart", touch(100));
  await main.dispatchEvent("touchmove", touch(200));
  await main.dispatchEvent("touchend", { touches: [] });
  expect((await reload).ok()).toBe(true);
  await page.goto(`/detail/${encodeURIComponent(fixturePath)}`);
  await page.reload();
  await page.getByRole("button", { name: "Play on TV", exact: true }).click();
  await page.getByRole("button", { name: /Resume/ }).click();
  await expect
    .poll(async () => (await getSession(request)).intent.pendingSeek?.positionSeconds)
    .toBe(40);
});

test("completed episode offers the next file without autoplay", async ({ page, request }) => {
  const episodes = ["Example.S01E01.mp4", "Example.S01E02.mp4"];
  const headers = { "X-Secret-Key": secretKey };
  try {
    for (const name of episodes) {
      await writeFile(resolve("e2e/fixtures/media", name), "episode fixture");
    }
    expect((await request.post(`${serverUrl}/api/library/rescan`, { headers })).ok()).toBe(true);
    await page.goto(connectUrl);
    await expect(page).toHaveURL(/\/library$/);
    const started = await request.post(`${serverUrl}/api/playback/session`, {
      headers,
      data: { relativePath: episodes[0], startPositionSeconds: 0 },
    });
    expect(started.ok()).toBe(true);
    const current = await getSession(request);
    await reportObservation(request, current, "ended", 120);
    await page.goto("/now-playing");
    const next = page.getByRole("button", { name: "Play next: Example S01E02" });
    await expect(next).toBeVisible();
    expect((await getSession(request)).intent.relativePath).toBe(episodes[0]);
    await next.click();
    await expect
      .poll(async () => (await getSession(request)).intent.relativePath)
      .toBe(episodes[1]);
  } finally {
    for (const name of episodes) {
      await unlink(resolve("e2e/fixtures/media", name)).catch(() => {});
    }
    await request.post(`${serverUrl}/api/library/rescan`, { headers });
  }
});
