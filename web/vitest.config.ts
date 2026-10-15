import { defineVitestConfig } from "@nuxt/test-utils/config";

export default defineVitestConfig({
  test: {
    environment: "nuxt",
    exclude: ["e2e/**", "node_modules/**"],
    setupFiles: ["tests/support/setup.ts"],
    unstubGlobals: true,
    // Node's own experimental localStorage would otherwise shadow the DOM one.
    poolOptions: { forks: { execArgv: ["--no-experimental-webstorage"] } },
  },
});
