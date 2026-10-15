import { beforeEach, describe, expect, it, vi } from "vitest";
import { readBrowseCache, writeBrowseCache } from "~~/app/lib/browse-cache";

describe("browse snapshots", () => {
  beforeEach(() => {
    vi.useRealTimers();
  });
  it("expires after one day", () => {
    vi.useFakeTimers();
    writeBrowseCache("build", "library", { name: "saved" });
    vi.advanceTimersByTime(86_399_999);
    expect(readBrowseCache("build", "library")).toEqual({ name: "saved" });
    vi.advanceTimersByTime(1);
    expect(readBrowseCache("build", "library")).toBeNull();
    vi.useRealTimers();
  });
  it("ignores malformed saved data", () => {
    writeBrowseCache("build", "library", { name: "saved" });
    const key = Object.keys(localStorage).find((name) => name.endsWith(":library"))!;
    for (const malformed of ["broken", "null", '{"data":{}}', '{"savedAt":"soon","data":{}}']) {
      localStorage.setItem(key, malformed);
      expect(readBrowseCache("build", "library")).toBeNull();
    }
  });
  it("starts afresh after a deploy, discarding the previous build's snapshots", () => {
    writeBrowseCache("build", "library", { name: "old build" });

    expect(readBrowseCache("next-build", "library")).toBeNull();
    writeBrowseCache("next-build", "favourites", []);

    expect(readBrowseCache("build", "library")).toBeNull();
  });
});
