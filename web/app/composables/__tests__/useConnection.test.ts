// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);

const storage = new Map<string, string>();
vi.stubGlobal("localStorage", {
  clear: () => storage.clear(),
  getItem: (key: string) => storage.get(key) ?? null,
  removeItem: (key: string) => storage.delete(key),
  setItem: (key: string, value: string) => storage.set(key, value),
});

// Mock Rivet modules
const mockConfigureApi = vi.fn();
const mockRivetConnect = vi.fn();

vi.mock("~~/app/lib/api", () => ({
  configureApi: (...args: unknown[]) => mockConfigureApi(...args),
  validateApiConnection: (...args: unknown[]) => mockRivetConnect(...args),
}));

// Import after mocks are set up
const { useConnection } = await import("../useConnection");

describe("useConnection", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("isConnected is true only when both serverUrl and secretKey are set", () => {
    const { isConnected, serverUrl, secretKey } = useConnection();

    expect(isConnected.value).toBe(false);

    serverUrl.value = "http://localhost:8080";
    expect(isConnected.value).toBe(false);

    secretKey.value = "my-key";
    expect(isConnected.value).toBe(true);

    serverUrl.value = "";
    expect(isConnected.value).toBe(false);
  });

  it("connect() calls Rivet connect and persists to localStorage on success", async () => {
    mockRivetConnect.mockResolvedValue({ status: "connected", serverName: null });

    const { connect, serverUrl, secretKey, isConnected } = useConnection();

    await connect("http://10.0.0.1:8080", "secret123");

    expect(mockConfigureApi).toHaveBeenCalledWith("http://10.0.0.1:8080", "secret123");
    expect(mockRivetConnect).toHaveBeenCalledWith("http://10.0.0.1:8080", "secret123");
    expect(localStorage.getItem("reel:serverUrl")).toBe("http://10.0.0.1:8080");
    expect(localStorage.getItem("reel:secretKey")).toBe("secret123");
    expect(serverUrl.value).toBe("http://10.0.0.1:8080");
    expect(secretKey.value).toBe("secret123");
    expect(isConnected.value).toBe(true);

  });

  it("connect() throws and does not persist when Rivet connect fails", async () => {
    mockRivetConnect.mockRejectedValue(new Error("Unauthorized"));

    const { connect, serverUrl, secretKey } = useConnection();

    await expect(connect("http://10.0.0.1:8080", "bad-key")).rejects.toThrow("Unauthorized");

    expect(localStorage.getItem("reel:serverUrl")).toBeNull();
    expect(localStorage.getItem("reel:secretKey")).toBeNull();
    expect(serverUrl.value).toBe("");
    expect(secretKey.value).toBe("");
  });

  it("failed reconnect preserves the working client and persisted connection", async () => {
    mockRivetConnect.mockResolvedValueOnce({ status: "connected", serverName: null });
    const { connect, serverUrl, secretKey } = useConnection();
    await connect("http://working:8080", "working-key");
    vi.clearAllMocks();
    mockRivetConnect.mockRejectedValueOnce(new Error("Unauthorized"));

    await expect(connect("http://bad:8080", "bad-key")).rejects.toThrow("Unauthorized");

    expect(mockConfigureApi).not.toHaveBeenCalled();
    expect(serverUrl.value).toBe("http://working:8080");
    expect(secretKey.value).toBe("working-key");
    expect(localStorage.getItem("reel:serverUrl")).toBe("http://working:8080");
  });

  it("restore() reads from localStorage and configures Rivet when values exist", () => {
    localStorage.setItem("reel:serverUrl", "http://saved:8080");
    localStorage.setItem("reel:secretKey", "saved-key");

    const { restore, serverUrl, secretKey, isConnected } = useConnection();
    restore();

    expect(mockConfigureApi).toHaveBeenCalledWith("http://saved:8080", "saved-key");
    expect(serverUrl.value).toBe("http://saved:8080");
    expect(secretKey.value).toBe("saved-key");
    expect(isConnected.value).toBe(true);

  });

  it("restore() does nothing when localStorage is empty", () => {
    const { restore, serverUrl, secretKey } = useConnection();
    restore();

    expect(mockConfigureApi).not.toHaveBeenCalled();
    expect(serverUrl.value).toBe("");
    expect(secretKey.value).toBe("");
  });

  it("disconnect() clears localStorage and resets state", async () => {
    mockRivetConnect.mockResolvedValue({ status: "connected", serverName: null });

    const { connect, disconnect, serverUrl, secretKey, isConnected } = useConnection();

    await connect("http://10.0.0.1:8080", "secret123");
    vi.clearAllMocks();

    disconnect();

    expect(localStorage.getItem("reel:serverUrl")).toBeNull();
    expect(localStorage.getItem("reel:secretKey")).toBeNull();
    expect(serverUrl.value).toBe("");
    expect(secretKey.value).toBe("");
    expect(isConnected.value).toBe(false);
    expect(mockConfigureApi).toHaveBeenCalledWith("");
  });
});
