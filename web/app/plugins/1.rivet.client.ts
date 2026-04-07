import { configureRivet } from "~~/generated/rivet/rivet";

export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig();

  configureRivet({
    baseUrl: config.public.serverUrl,
  });
});
