export default defineNuxtRouteMiddleware((to) => {
  if (to.path !== "/connect" && !useConnection().isConnected.value) {
    return navigateTo("/connect", { replace: true });
  }
});
