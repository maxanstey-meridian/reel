import { defineConfig } from "@playwright/test";

const headless = process.env.E2E_HEADLESS !== "false";
const slowMo = Number(process.env.E2E_SLOWMO ?? 0);
const serverPort = process.env.E2E_SERVER_PORT ?? "18080";
const serverUrl = `http://127.0.0.1:${serverPort}`;
const webUrl = "http://127.0.0.1:3000";

export default defineConfig({
  testDir: "e2e",
  testMatch: "**/*.spec.ts",
  timeout: 60_000,

  use: {
    baseURL: webUrl,
    headless,
    launchOptions: { slowMo, executablePath: process.env.E2E_BROWSER_EXECUTABLE },
  },

  webServer: [
    {
      command:
        `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=${serverUrl} Reel__MediaRoot=$PWD/e2e/fixtures/media Reel__SecretKey=REEL-E2E Reel__Port=${serverPort} Reel__WebUrl=${webUrl} dotnet run --project ../server/Reel.Server/Reel.Server.csproj --no-launch-profile`,
      url: `${serverUrl}/api/health`,
      reuseExistingServer: false,
      timeout: 60_000,
    },
    {
      command: "NUXT_DEVTOOLS_ENABLED=false pnpm dev --host 127.0.0.1 --port 3000",
      url: webUrl,
      reuseExistingServer: false,
      timeout: 60_000,
    },
  ],
});
