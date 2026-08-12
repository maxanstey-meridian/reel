export default defineNuxtConfig({
  ssr: false,
  compatibilityDate: "2025-01-01",
  modules: ["@nuxt/ui", "@nuxt/eslint", "@vite-pwa/nuxt"],

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

  pwa: {
    registerType: "autoUpdate",
    manifest: {
      name: "Reel",
      short_name: "Reel",
      description: "Cast to your TV",
      theme_color: "#0a0a0a",
      background_color: "#0a0a0a",
      display: "standalone",
      orientation: "portrait",
      icons: [
        { src: "/icon-192.png", sizes: "192x192", type: "image/png" },
        { src: "/icon-512.png", sizes: "512x512", type: "image/png" },
        { src: "/icon-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
      ],
    },
    workbox: {
      navigateFallback: "/",
      globPatterns: ["**/*.{js,css,html,svg,png,ico,webmanifest}"],
    },
    devOptions: {
      enabled: false,
    },
  },
  css: ["~/assets/css/app.css"],

  vite: {
    optimizeDeps: {
      include: ["zod"],
    },
  },

  runtimeConfig: {
    public: {
      serverUrl: "http://localhost:8080",
    },
  },

  devServer: {
    port: 3000,
  },
});
