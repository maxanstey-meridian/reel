import { describe, it, expect } from "vitest";
import { ALPHABET, getNameInitial } from "~~/app/utils/alphabetScroller";

describe("ALPHABET", () => {
  it("contains 27 entries (# plus A-Z)", () => {
    expect(ALPHABET).toHaveLength(27);
  });

  it("starts with # and ends with Z", () => {
    expect(ALPHABET[0]).toBe("#");
    expect(ALPHABET[26]).toBe("Z");
  });

  it("has consecutive letters A through Z after #", () => {
    for (let i = 1; i < 27; i++) {
      expect(ALPHABET[i]).toBe(String.fromCharCode(64 + i));
    }
  });
});

describe("getNameInitial", () => {
  it("returns uppercase letter for alphabetic names", () => {
    expect(getNameInitial("Avatar")).toBe("A");
    expect(getNameInitial("avatar")).toBe("A");
    expect(getNameInitial("Zorro")).toBe("Z");
  });

  it("returns # for numeric names", () => {
    expect(getNameInitial("2001: A Space Odyssey")).toBe("#");
    expect(getNameInitial("007.mp4")).toBe("#");
  });

  it("returns # for special character names", () => {
    expect(getNameInitial("(Untitled)")).toBe("#");
    expect(getNameInitial("_hidden")).toBe("#");
    expect(getNameInitial(".dotfile")).toBe("#");
  });

  it("returns # for empty string", () => {
    expect(getNameInitial("")).toBe("#");
  });
});
