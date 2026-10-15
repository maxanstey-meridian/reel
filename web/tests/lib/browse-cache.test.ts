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
    localStorage.setItem("reel:browse:v1:library", "broken");
    expect(readBrowseCache("library")).toBeNull();
  });
});
