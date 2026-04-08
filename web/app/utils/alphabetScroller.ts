export const ALPHABET = [
  "#",
  "A",
  "B",
  "C",
  "D",
  "E",
  "F",
  "G",
  "H",
  "I",
  "J",
  "K",
  "L",
  "M",
  "N",
  "O",
  "P",
  "Q",
  "R",
  "S",
  "T",
  "U",
  "V",
  "W",
  "X",
  "Y",
  "Z",
] as const;

export const getNameInitial = (name: string): string => {
  const first = name.charAt(0).toUpperCase();
  if (first >= "A" && first <= "Z") {
    return first;
  }
  return "#";
};

export const SCROLLER_MIN_ITEMS = 10;

export const isScrollerVisible = (count: number): boolean => count > SCROLLER_MIN_ITEMS;

export const scrollToNameInitial = (letter: string): void => {
  const el = document.querySelector(`[data-name-initial="${letter}"]`);
  if (el) {
    el.scrollIntoView({ block: "start", behavior: "smooth" });
  }
};
