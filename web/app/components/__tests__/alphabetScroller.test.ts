// @vitest-environment jsdom

import { describe, it, expect, vi, afterEach } from "vitest";
import {
  ALPHABET,
  SCROLLER_MIN_ITEMS,
  isScrollerVisible,
  scrollToNameInitial,
} from "~~/app/utils/alphabetScroller";

/**
 * Tests for the actual exported functions used by AlphabetScroller.vue
 * and library.vue. These guard the real implementation, not copies.
 */

// ── Visibility threshold ──

describe("isScrollerVisible", () => {
  it("returns false when count is at or below threshold", () => {
    expect(isScrollerVisible(0)).toBe(false);
    expect(isScrollerVisible(1)).toBe(false);
    expect(isScrollerVisible(SCROLLER_MIN_ITEMS)).toBe(false);
  });

  it("returns true when count exceeds threshold", () => {
    expect(isScrollerVisible(SCROLLER_MIN_ITEMS + 1)).toBe(true);
    expect(isScrollerVisible(50)).toBe(true);
  });

  it("threshold is 10", () => {
    expect(SCROLLER_MIN_ITEMS).toBe(10);
  });
});

// ── ALPHABET constant ──

describe("ALPHABET constant", () => {
  it("provides all 27 entries for the scroller buttons", () => {
    expect(ALPHABET).toHaveLength(27);
    expect(ALPHABET[0]).toBe("#");
    expect(ALPHABET[1]).toBe("A");
    expect(ALPHABET[26]).toBe("Z");
  });
});

// ── scrollToNameInitial ──

describe("scrollToNameInitial", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("queries data-name-initial and calls scrollIntoView on matching element", () => {
    const mockScrollIntoView = vi.fn();
    const mockElement = { scrollIntoView: mockScrollIntoView };

    vi.spyOn(document, "querySelector").mockReturnValue(mockElement as unknown as Element);

    scrollToNameInitial("M");

    expect(document.querySelector).toHaveBeenCalledWith('[data-name-initial="M"]');
    expect(mockScrollIntoView).toHaveBeenCalledWith({ block: "start", behavior: "smooth" });
  });

  it("does not throw when no matching element exists", () => {
    vi.spyOn(document, "querySelector").mockReturnValue(null);

    expect(() => scrollToNameInitial("X")).not.toThrow();
  });

  it("passes the letter through to the selector exactly", () => {
    vi.spyOn(document, "querySelector").mockReturnValue(null);

    scrollToNameInitial("#");

    expect(document.querySelector).toHaveBeenCalledWith('[data-name-initial="#"]');
  });
});
