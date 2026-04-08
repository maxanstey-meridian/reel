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
