import { beforeEach } from "vitest";

// Each test starts with no saved pairing or browse data, no shared fetches and no toasts.
beforeEach(() => {
  localStorage.clear();
  clearNuxtData();
  useToast().clear();
});
