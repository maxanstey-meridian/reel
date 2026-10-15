import { beforeEach, describe, expect, it, vi } from "vitest";
import { readBrowseCache, writeBrowseCache } from "~~/app/lib/browse-cache";

describe("browse snapshots", () => {
  beforeEach(() => {
    vi.useRealTimers();
  });
  it("expires after one day", () => {
    vi.useFakeTimers();
    writeBrowseCache("library", { name: "saved" });
    vi.advanceTimersByTime(86_399_999);
    expect(readBrowseCache("library")).toEqual({ name: "saved" });
    vi.advanceTimersByTime(1);
    expect(readBrowseCache("library")).toBeNull();
    vi.useRealTimers();
  });
  it("ignores malformed saved data", () => {
    writeBrowseCache("library", { name: "saved" });
    const key = Object.keys(localStorage).find((name) => name.endsWith(":library"))!;
    for (const malformed of ["broken", "null", '{"data":{}}', '{"savedAt":"soon","data":{}}']) {
      localStorage.setItem(key, malformed);
      expect(readBrowseCache("library")).toBeNull();
    }
  });
  it("starts afresh after a deploy, discarding the previous build's snapshots", () => {
    const config = useRuntimeConfig().app;
    const build = config.buildId;
    writeBrowseCache("library", { name: "old build" });
    try {
      config.buildId = "next-build";
      expect(readBrowseCache("library")).toBeNull();
      writeBrowseCache("favourites", []);
    } finally {
      config.buildId = build;
    }
    expect(readBrowseCache("library")).toBeNull();
  });
});
