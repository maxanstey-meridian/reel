// Regex match groups and array-last access are runtime-safe but TS can't narrow them.
// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-nocheck

const EXTENSIONS_RE = /\.(mp4|mkv|avi|mov|wmv|flv|webm|m4v|ts|divx|ogm)$/i;

const TAGS = [
  "1080p",
  "720p",
  "2160p",
  "4K",
  "UHD",
  "BluRay",
  "Blu-Ray",
  "BRRip",
  "WEBRip",
  "WEB-DL",
  "WEBDL",
  "HDRip",
  "DVDRip",
  "x264",
  "x265",
  "H264",
  "H265",
  "HEVC",
  "AAC",
  "AC3",
  "DTS",
  "YIFY",
  "RARBG",
  "10bit",
  "HDR",
  "SDR",
  "REMUX",
  "PROPER",
];

// Build a single regex that matches any known tag as a whole word (case-insensitive).
const TAGS_RE = new RegExp(
  `\\b(${TAGS.map((t) => t.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|")})\\b`,
  "gi",
);

const YEAR_PAREN_RE = /\((\d{4})\)/g;
const YEAR_BARE_RE = /(?<=\s|^)(19[2-9]\d|20[0-9]\d)(?=\s|$)/g;

export const parseFilename = (filename: string): { title: string; year: number | null } => {
  // 1. Strip file extension
  let working = filename.replace(EXTENSIONS_RE, "");

  // 2. Replace dots and underscores with spaces early so \b works for tags
  working = working.replace(/[._]/g, " ");

  // 3. Extract parenthesised year like "(2010)" — prefer this form
  let year: number | null = null;
  let yearIndex = -1;

  const parenMatches = [...working.matchAll(YEAR_PAREN_RE)];
  for (const m of parenMatches) {
    const y = parseInt(m[1], 10);
    if (y >= 1920 && y <= 2099 && m.index !== undefined && m.index > yearIndex) {
      year = y;
      yearIndex = m.index;
    }
  }

  if (year !== null) {
    // Remove the parenthesised year including parens
    working = working.slice(0, yearIndex) + working.slice(yearIndex + `(${year})`.length);
  } else {
    // 4. Find bare years — pick the last one whose remainder is only tags/whitespace.
    // This correctly handles "2001 A Space Odyssey 1968 REMUX" → year=1968, title includes "2001".
    const bareMatches = [...working.matchAll(YEAR_BARE_RE)];

    // Scan all candidates. Prefer the last year where everything after it is tags.
    let bestYear: number | null = null;
    let bestIndex = -1;

    for (const m of bareMatches) {
      const y = parseInt(m[1], 10);
      if (y >= 1920 && y <= 2099 && m.index !== undefined) {
        const after = working
          .slice(m.index + m[1].length)
          .replace(TAGS_RE, "")
          .trim();

        if (after === "") {
          // Everything after is tags — this is a strong candidate
          bestYear = y;
          bestIndex = m.index;
        }
      }
    }

    // If no year had a tag-only remainder, fall back to the last bare year
    if (bestYear === null && bareMatches.length > 0) {
      const last = bareMatches[bareMatches.length - 1];
      const y = parseInt(last[1], 10);
      if (y >= 1920 && y <= 2099 && last.index !== undefined) {
        bestYear = y;
        bestIndex = last.index;
      }
    }

    if (bestYear !== null && bestIndex >= 0) {
      year = bestYear;
      // Truncate everything from the year onward (year + tags)
      working = working.slice(0, bestIndex);
    }
  }

  // 5. Strip any remaining known tags (handles no-year cases like "movie name 720p WEBRip")
  working = working.replace(TAGS_RE, "");

  // 6. Replace hyphens between word characters with spaces (but known tags like WEB-DL already stripped)
  working = working.replace(/(?<=\w)-(?=\w)/g, " ");

  // 7. Collapse whitespace and trim
  working = working.replace(/\s+/g, " ").trim();

  // 8. Fallback for degenerate inputs
  if (!working) {
    working = filename.replace(EXTENSIONS_RE, "").replace(/[._]/g, " ").trim();
  }
  if (!working) {
    working = filename;
  }

  return { title: working, year };
};
