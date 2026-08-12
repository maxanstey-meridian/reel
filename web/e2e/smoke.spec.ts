import { test, expect } from "@playwright/test";

const serverUrl = `http://127.0.0.1:${process.env.E2E_SERVER_PORT ?? "18080"}`;
const connectUrl = `/connect?url=${encodeURIComponent(serverUrl)}&key=REEL-E2E`;

test.describe("Smoke", () => {
  test("app redirects to connect page", async ({ page }) => {
    await page.goto("/");
    await expect(page).toHaveURL(/\/connect/);
    await expect(page.locator("h1")).toContainText("Reel");
  });
});

test.describe("Layout", () => {
  test("connect page has no bottom nav", async ({ page }) => {
    await page.goto("/connect");
    await expect(page.getByTestId("bottom-nav")).not.toBeVisible();
  });

  test("bottom nav is visible on pages using default layout", async ({ page }) => {
    await page.goto(connectUrl);
    await expect(page).toHaveURL(/\/library$/);
    const nav = page.getByTestId("bottom-nav");
    await expect(nav).toBeVisible();
    await expect(nav.locator("a")).toHaveCount(3);
  });

  test("active nav item is highlighted", async ({ page }) => {
    await page.goto(connectUrl);
    await expect(page).toHaveURL(/\/library$/);
    const libraryLink = page.getByTestId("bottom-nav").locator('a[href="/library"]');
    await expect(libraryLink).toHaveAttribute("data-active", "true");
  });
});

test.describe("Connection isolation", () => {
  test("failed reconnect keeps the existing working connection", async ({ page }) => {
    await page.goto(connectUrl);
    await expect(page).toHaveURL(/\/library$/);

    await page.goto("/connect");
    await page.getByLabel("Server URL").fill(serverUrl);
    await page.getByLabel("Secret Key").fill("WRONG-KEY");
    await page.getByRole("button", { name: "Connect", exact: true }).click();
    await expect(page.getByText("API request failed with status 401", { exact: true })).toBeVisible();
    await page.goto("/library");
    await expect(page).toHaveURL(/\/library$/);
    await expect(page.getByTestId("bottom-nav")).toBeVisible();
  });
});
