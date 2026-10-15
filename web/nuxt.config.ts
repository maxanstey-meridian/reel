export default defineNuxtConfig({
  ssr: false,
  compatibilityDate: "2025-01-01",
  modules: ["@nuxt/ui", "@nuxt/eslint"],

  app: {
    head: {
      viewport: "width=device-width, initial-scale=1, viewport-fit=cover",
      link: [
        { rel: "apple-touch-icon", href: "/apple-touch-icon.png" },
        { rel: "icon", href: "/icon.svg", type: "image/svg+xml" },
      ],
      meta: [
        { name: "apple-mobile-web-app-capable", content: "yes" },
        { name: "apple-mobile-web-app-status-bar-style", content: "black-translucent" },
        { name: "apple-mobile-web-app-title", content: "Reel" },
        { name: "theme-color", content: "#0a0a0a" },
      ],
    },
  },

  css: ["~/assets/css/app.css"],

  devServer: {
    port: 3000,
  },

  // The phone app talks to its own origin; in development that origin is this server, so the
  // API is forwarded to the .NET server (override with REEL_API_URL, e.g. for e2e runs).
  nitro: {
    devProxy: {
      "/api": {
        target: `${process.env.REEL_API_URL ?? "http://localhost:8080"}/api`,
        changeOrigin: true,
      },
    },
  },

  typescript: {
    tsConfig: { include: ["../tests/**/*", "../e2e/**/*", "../playwright.config.ts"] },
  },
});
