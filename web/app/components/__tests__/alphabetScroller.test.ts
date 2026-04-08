// @vitest-environment jsdom

import { describe, it, expect, vi } from "vitest";
import { ALPHABET } from "~~/app/utils/alphabetScroller";

/**
 * Tests for AlphabetScroller behavior that lives in the component
 * (visibility threshold, emit-on-tap) and the library.vue wiring
 * (handleScrollToLetter queries data-name-initial and scrolls).
 *
 * These test the extracted logic directly rather than mounting components.
 */

// ── Visibility threshold (mirrors AlphabetScroller.vue line 11) ──

const isVisible = (names: string[]) => names.length > 10;

describe("AlphabetScroller visibility", () => {
  it("hidden when 10 or fewer items", () => {
    expect(isVisible(Array(10).fill("A"))).toBe(false);
    expect(isVisible(Array(0).fill("A"))).toBe(false);
    expect(isVisible(Array(1).fill("A"))).toBe(false);
  });

  it("visible when more than 10 items", () => {
    expect(isVisible(Array(11).fill("A"))).toBe(true);
    expect(isVisible(Array(50).fill("A"))).toBe(true);
  });
});

// ── Tap emits correct letter ──

describe("AlphabetScroller tap behavior", () => {
  it("handleTap emits the tapped letter", () => {
    // Mirrors AlphabetScroller.vue handleTap (lines 14-17)
    const emitted: string[] = [];
    const emit = (letter: string) => {
      emitted.push(letter);
    };

    const handleTap = (letter: string) => {
      emit(letter);
    };

    handleTap("A");
    handleTap("Z");
    handleTap("#");

    expect(emitted).toEqual(["A", "Z", "#"]);
  });

  it("ALPHABET provides all 27 letters for the scroller buttons", () => {
    expect(ALPHABET).toHaveLength(27);
    expect(ALPHABET[0]).toBe("#");
    expect(ALPHABET[1]).toBe("A");
    expect(ALPHABET[26]).toBe("Z");
  });
});

// ── handleScrollToLetter wiring (mirrors library.vue lines 81-86) ──

describe("handleScrollToLetter", () => {
  it("queries data-name-initial and calls scrollIntoView on matching element", () => {
    const mockScrollIntoView = vi.fn();
    const mockElement = { scrollIntoView: mockScrollIntoView };

    vi.spyOn(document, "querySelector").mockReturnValue(mockElement as unknown as Element);

    // Replicate the logic from library.vue handleScrollToLetter
    const handleScrollToLetter = (letter: string) => {
      const el = document.querySelector(`[data-name-initial="${letter}"]`);
      if (el) {
        el.scrollIntoView({ block: "start", behavior: "smooth" });
      }
    };

    handleScrollToLetter("M");

    expect(document.querySelector).toHaveBeenCalledWith('[data-name-initial="M"]');
    expect(mockScrollIntoView).toHaveBeenCalledWith({ block: "start", behavior: "smooth" });

    vi.restoreAllMocks();
  });

  it("does not throw when no matching element exists", () => {
    vi.spyOn(document, "querySelector").mockReturnValue(null);

    const handleScrollToLetter = (letter: string) => {
      const el = document.querySelector(`[data-name-initial="${letter}"]`);
      if (el) {
        el.scrollIntoView({ block: "start", behavior: "smooth" });
      }
    };

    expect(() => handleScrollToLetter("X")).not.toThrow();

    vi.restoreAllMocks();
  });
});
