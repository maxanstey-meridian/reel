import { test, expect } from "@playwright/test";

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
    await page.goto("/library");
    const nav = page.getByTestId("bottom-nav");
    await expect(nav).toBeVisible();
    await expect(nav.locator("a")).toHaveCount(3);
  });

  test("active nav item is highlighted", async ({ page }) => {
    await page.goto("/library");
    const libraryLink = page.getByTestId("bottom-nav").locator('a[href="/library"]');
    await expect(libraryLink).toHaveAttribute("data-active", "true");
  });
});
