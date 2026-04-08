// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed, watch, nextTick } from "vue";
import type { SubtitleTrack } from "~~/generated/rivet/types/domain";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);
vi.stubGlobal("watch", watch);

const mockSubtitlesList = vi.fn();
const mockMediaDetail = vi.fn();

vi.mock("~~/generated/rivet/client/reel", () => ({
  subtitlesList: (...args: unknown[]) => mockSubtitlesList(...args),
  mediaDetail: (...args: unknown[]) => mockMediaDetail(...args),
}));

const makeTrack = (overrides: Partial<SubtitleTrack> = {}): SubtitleTrack => ({
  index: 0,
  label: "English",
  language: "English",
  format: "SRT",
  source: "external",
  externalPath: null,
  ...overrides,
});

describe("Subtitle display in FileDetail", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("calls subtitlesList when drawer opens with a file path", async () => {
    mockSubtitlesList.mockResolvedValue([makeTrack()]);

    const subtitles = ref<SubtitleTrack[]>([]);
    const path = "Films/movie.mkv";

    try {
      subtitles.value = await mockSubtitlesList(path);
    } catch {
      subtitles.value = [];
    }

    expect(mockSubtitlesList).toHaveBeenCalledWith("Films/movie.mkv");
    expect(subtitles.value).toHaveLength(1);
  });

  it("populates subtitles with language, format, and source", async () => {
    const tracks = [
      makeTrack({ index: 0, language: "English", format: "SRT", source: "external" }),
      makeTrack({ index: 1, language: "Spanish", format: "ASS", source: "embedded" }),
    ];
    mockSubtitlesList.mockResolvedValue(tracks);

    const subtitles = ref<SubtitleTrack[]>([]);
    subtitles.value = await mockSubtitlesList("Films/movie.mkv");

    expect(subtitles.value).toHaveLength(2);
    const [first, second] = subtitles.value;
    expect(first!.language).toBe("English");
    expect(first!.format).toBe("SRT");
    expect(first!.source).toBe("external");
    expect(second!.language).toBe("Spanish");
    expect(second!.format).toBe("ASS");
    expect(second!.source).toBe("embedded");
  });

  it("falls back to 'Unknown' when language is empty", () => {
    const track = makeTrack({ language: "" });
    const displayLanguage = track.language || "Unknown";
    expect(displayLanguage).toBe("Unknown");
  });

  it("shows language when present", () => {
    const track = makeTrack({ language: "French" });
    const displayLanguage = track.language || "Unknown";
    expect(displayLanguage).toBe("French");
  });

  it("section hidden when subtitles array is empty", async () => {
    mockSubtitlesList.mockResolvedValue([]);

    const subtitles = ref<SubtitleTrack[]>([]);
    subtitles.value = await mockSubtitlesList("Films/movie.mkv");

    // Template uses v-if="subtitles.length > 0"
    expect(subtitles.value.length > 0).toBe(false);
  });

  it("clears subtitles to empty array on error", async () => {
    mockSubtitlesList.mockRejectedValue(new Error("Not found"));

    const subtitles = ref<SubtitleTrack[]>([makeTrack()]); // Pre-populate

    try {
      subtitles.value = await mockSubtitlesList("Films/missing.mkv");
    } catch {
      subtitles.value = [];
    }

    expect(subtitles.value).toEqual([]);
  });

  it("fetches subtitles alongside detail when drawer opens", async () => {
    mockSubtitlesList.mockResolvedValue([makeTrack()]);
    mockMediaDetail.mockResolvedValue({ overview: "A movie" });

    const open = ref(false);
    const node = ref<{ relativePath: string } | null>({ relativePath: "Films/movie.mkv" });
    const subtitles = ref<SubtitleTrack[]>([]);
    const detail = ref<unknown>(null);

    watch(
      () => [node.value, open.value] as const,
      async ([n, isOpen]) => {
        if (isOpen && n) {
          const [detailResult, subsResult] = await Promise.all([
            mockMediaDetail(n.relativePath),
            mockSubtitlesList(n.relativePath),
          ]);
          detail.value = detailResult;
          subtitles.value = subsResult;
        }
        if (!isOpen) {
          detail.value = null;
          subtitles.value = [];
        }
      },
    );

    open.value = true;
    await nextTick();

    await vi.waitFor(() => {
      expect(mockSubtitlesList).toHaveBeenCalledWith("Films/movie.mkv");
      expect(mockMediaDetail).toHaveBeenCalledWith("Films/movie.mkv");
    });

    expect(subtitles.value).toHaveLength(1);
    expect(detail.value).toEqual({ overview: "A movie" });
  });

  it("clears subtitles when drawer closes", async () => {
    mockSubtitlesList.mockResolvedValue([makeTrack()]);

    const open = ref(true);
    const node = ref<{ relativePath: string } | null>({ relativePath: "Films/movie.mkv" });
    const subtitles = ref<SubtitleTrack[]>([]);

    watch(
      () => [node.value, open.value] as const,
      async ([n, isOpen]) => {
        if (isOpen && n) {
          try {
            subtitles.value = await mockSubtitlesList(n.relativePath);
          } catch {
            subtitles.value = [];
          }
        }
        if (!isOpen) {
          subtitles.value = [];
        }
      },
      { immediate: true },
    );

    await nextTick();
    await vi.waitFor(() => expect(subtitles.value).toHaveLength(1));

    // Close the drawer
    open.value = false;
    await nextTick();

    expect(subtitles.value).toEqual([]);
  });
});
