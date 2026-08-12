import { expect, test, type APIRequestContext } from "@playwright/test";

const serverUrl = `http://127.0.0.1:${process.env.E2E_SERVER_PORT ?? "18080"}`;
const secretKey = "REEL-E2E";
const fixturePath = "Browser E2E Fixture.mp4";

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
  state: "playing" | "paused" | "idle",
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
      positionSeconds: state === "idle" ? 0 : 12,
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
    await page.goto(`/connect?url=${encodeURIComponent(serverUrl)}&key=${secretKey}`);
    await expect(page).toHaveURL(/\/library$/);

    await page.goto("/search");
    await page.getByPlaceholder("Search by title...").fill("Browser E2E Fixture");
    await page.getByText("Browser E2E Fixture", { exact: true }).click();
    await expect(page.getByRole("heading", { name: "Browser E2E Fixture" })).toBeVisible();

    await page.getByRole("button", { name: "Play", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Now Playing" })).toBeVisible();
    const nowPlaying = page.locator("main");
    await expect(nowPlaying.getByText("TV disconnected", { exact: true })).toBeVisible();

    let session = await getSession(request);
    expect(session.intent.relativePath).toBe(fixturePath);
    expect(session.intent.desiredState).toBe("playing");
    await reportObservation(request, session, "playing");
    await expect(nowPlaying.getByText("playing", { exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Pause playback" }).click();
    await expect(page.getByText("Waiting for TV to apply control…")).toBeVisible();
    await expect.poll(async () => (await getSession(request)).intent.desiredState).toBe("paused");
    session = await getSession(request);
    await reportObservation(request, session, "paused");
    await expect(nowPlaying.getByText("paused", { exact: true })).toBeVisible();

    await page.getByRole("button", { name: "Stop playback" }).click();
    await expect(page.getByText("Waiting for TV to apply control…")).toBeVisible();
    await expect.poll(async () => (await getSession(request)).intent.desiredState).toBe("stopped");
    session = await getSession(request);
    await reportObservation(request, session, "idle");
    await expect(nowPlaying.getByText("idle", { exact: true })).toBeVisible();
    await expect(nowPlaying.getByText("playing", { exact: true })).not.toBeVisible();
  });
});
