import type { Page } from "@playwright/test";

/**
 * Returns a promise that resolves when a response matching the URL pattern completes.
 * Use to verify API calls succeed before asserting UI state.
 *
 * Usage:
 *   const spy = spyResponse(page, '/api/library');
 *   await page.click('button');
 *   const response = await spy;
 *   expect(response.status()).toBe(200);
 */
export const spyResponse = (page: Page, urlPattern: string) =>
  page.waitForResponse((r) => r.url().includes(urlPattern) && r.status() < 400);
