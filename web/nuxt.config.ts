export default defineNuxtConfig({
  ssr: false,
  compatibilityDate: "2025-01-01",
  modules: ["@nuxt/ui", "@nuxt/eslint"],
  css: ["./app/assets/css/app.css"],

  runtimeConfig: {
    public: {
      serverUrl: "http://localhost:8080",
    },
  },

  devServer: {
    port: 3000,
  },
});
