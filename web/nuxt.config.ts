export default defineNuxtConfig({
  ssr: false,
  compatibilityDate: "2025-01-01",
  modules: ["@nuxt/ui", "@nuxt/eslint"],

  app: {
    head: {
      viewport: "width=device-width, initial-scale=1, viewport-fit=cover",
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
