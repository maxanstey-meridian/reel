import { defineConfig } from "@playwright/test";

const headless = process.env.E2E_HEADLESS !== "false";
const slowMo = Number(process.env.E2E_SLOWMO ?? 0);

export default defineConfig({
  testDir: "e2e",
  testMatch: "**/*.spec.ts",
  timeout: 60_000,

  use: {
    baseURL: "http://localhost:3000",
    headless,
    launchOptions: { slowMo },
  },

  webServer: {
    command: "pnpm dev",
    url: "http://localhost:3000",
    reuseExistingServer: !process.env.CI,
    timeout: 30_000,
  },
});
