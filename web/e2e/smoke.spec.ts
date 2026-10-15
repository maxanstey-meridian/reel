import { test, expect } from "@playwright/test";

const connectUrl = "/connect?key=REEL-E2E";

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
    await expect(nav.locator("a")).toHaveCount(5);
  });

  test("active nav item is highlighted", async ({ page }) => {
    await page.goto(connectUrl);
    await expect(page).toHaveURL(/\/library$/);
    const libraryLink = page.getByTestId("bottom-nav").locator('a[href="/library"]');
    await expect(libraryLink).toHaveAttribute("data-active", "true");
  });
});

test.describe("Connection isolation", () => {
  test("a wrong pairing code keeps the existing working pairing", async ({ page }) => {
    await page.goto(connectUrl);
    await expect(page).toHaveURL(/\/library$/);

    await page.goto("/connect");
    await page.getByLabel("Pairing code shown on your TV").fill("WRONG-KEY");
    await page.getByRole("button", { name: "Pair", exact: true }).click();
    await expect(page.getByText("That code doesn't match your TV.", { exact: true })).toBeVisible();
    await page.goto("/library");
    await expect(page).toHaveURL(/\/library$/);
    await expect(page.getByTestId("bottom-nav")).toBeVisible();
  });
});
