import { defineConfig } from "@playwright/test";

const headless = process.env.E2E_HEADLESS !== "false";
const slowMo = Number(process.env.E2E_SLOWMO ?? 0);
const serverPort = process.env.E2E_SERVER_PORT ?? "18080";
const serverUrl = `http://127.0.0.1:${serverPort}`;
const published = process.env.E2E_PUBLISHED_DIR;
const webUrl = published ? serverUrl : "http://127.0.0.1:3000";

export default defineConfig({
  testDir: "e2e",
  workers: 1, // All journeys use the same single TV session.
  testMatch: "**/*.spec.ts",
  timeout: 60_000,

  use: {
    baseURL: webUrl,
    viewport: { width: 390, height: 844 },
    isMobile: true,
    hasTouch: true,
    headless,
    launchOptions: { slowMo, executablePath: process.env.E2E_BROWSER_EXECUTABLE },
  },

  webServer: [
    {
      command: `ASPNETCORE_ENVIRONMENT=${published ? "Production" : "Development"} Reel__MediaRoot=$PWD/e2e/fixtures/media Reel__SecretKey=REEL-E2E Reel__Port=${serverPort} Reel__ServerUrl=${serverUrl} Reel__WebUrl=${webUrl} ${published ? `dotnet "${published}/Reel.Server.dll" --contentRoot "${published}"` : "dotnet run --project ../server/Reel.Server/Reel.Server.csproj --no-launch-profile"}`,
      url: `${serverUrl}/api/health`,
      reuseExistingServer: false,
      timeout: 60_000,
    },
    ...(published
      ? []
      : [
          {
            command: `NUXT_DEVTOOLS_ENABLED=false REEL_API_URL=${serverUrl} pnpm dev --host 127.0.0.1 --port 3000`,
            url: webUrl,
            reuseExistingServer: false,
            timeout: 60_000,
          },
        ]),
  ],
});
