// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal(
  "useState",
  <T>(_key: string, init: () => T) => ref(init()),
);
vi.stubGlobal("computed", computed);

// Mock useConnection
const mockServerUrl = ref("http://localhost:8080");
const mockSecretKey = ref("test-key");

vi.mock("../useConnection", () => ({
  useConnection: () => ({
    serverUrl: mockServerUrl,
    secretKey: mockSecretKey,
  }),
}));

// Mock global fetch
const mockFetch = vi.fn();
vi.stubGlobal("fetch", mockFetch);

// Import after mocks
const { useProgress } = await import("../useProgress");

describe("useProgress", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetch loads progress entries", async () => {
    const serverEntries = [
      {
        relativePath: "Films/recent.mp4",
        positionSeconds: 3600,
        durationSeconds: 7200,
        lastWatched: "2026-04-07T10:00:00Z",
      },
      {
        relativePath: "Films/older.mp4",
        positionSeconds: 1800,
        durationSeconds: 5400,
        lastWatched: "2026-04-06T10:00:00Z",
      },
    ];

    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve(serverEntries),
    });

    const { entries, fetch } = useProgress();
    await fetch();

    expect(entries.value).toEqual(serverEntries);
    expect(entries.value).toHaveLength(2);
    expect(mockFetch).toHaveBeenCalledWith(
      "http://localhost:8080/api/progress",
      expect.objectContaining({
        headers: expect.objectContaining({ "X-Secret-Key": "test-key" }),
      }),
    );
  });

  it("returns empty array when no progress", async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve([]),
    });

    const { entries, fetch } = useProgress();
    await fetch();

    expect(entries.value).toEqual([]);
  });
});
