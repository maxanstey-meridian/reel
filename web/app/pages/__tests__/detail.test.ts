// @vitest-environment jsdom

import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref, computed, watch } from "vue";

// Mock Nuxt auto-imports
vi.stubGlobal("useState", <T>(_key: string, init: () => T) => ref(init()));
vi.stubGlobal("computed", computed);
vi.stubGlobal("watch", watch);
vi.stubGlobal("onMounted", (fn: () => void) => fn());
const mockToastAdd = vi.fn();
vi.stubGlobal("useToast", () => ({ add: mockToastAdd }));

const mockNavigateTo = vi.fn();
vi.stubGlobal("navigateTo", mockNavigateTo);

// Mock useLibrary (used by useWatchState internally)
const mockRefresh = vi.fn().mockResolvedValue(undefined);
vi.stubGlobal("useLibrary", () => ({ refresh: mockRefresh }));

// Mock Rivet client
const mockMediaDetail = vi.fn();
const mockSubtitlesList = vi.fn();
const mockTagsList = vi.fn();
const mockTagsRemove = vi.fn();
const mockTagsAll = vi.fn();
const mockCast = vi.fn();
const mockCastStop = vi.fn();
const mockMediaClearWatched = vi.fn();
const mockThumbnailUrl = vi.fn().mockReturnValue("http://localhost/thumb");

vi.mock("~~/generated/rivet/client/reel", () => ({
  mediaDetail: (...args: unknown[]) => mockMediaDetail(...args),
  subtitlesList: (...args: unknown[]) => mockSubtitlesList(...args),
  tagsList: (...args: unknown[]) => mockTagsList(...args),
  tagsRemove: (...args: unknown[]) => mockTagsRemove(...args),
  tagsAll: (...args: unknown[]) => mockTagsAll(...args),
  cast: (...args: unknown[]) => mockCast(...args),
  castStop: (...args: unknown[]) => mockCastStop(...args),
  mediaClearWatched: (...args: unknown[]) => mockMediaClearWatched(...args),
  thumbnailUrl: (...args: unknown[]) => mockThumbnailUrl(...args),
}));

// Mock rivetFetch (used for PATCH and watched/unwatched)
const mockRivetFetch = vi.fn();
vi.mock("~~/generated/rivet/rivet", () => ({
  rivetFetch: (...args: unknown[]) => mockRivetFetch(...args),
}));

// Import composables after mocks
const { useCast } = await import("../../composables/useCast");
const { useWatchState } = await import("../../composables/useWatchState");
const { useTags } = await import("../../composables/useTags");
const { buildPatchBody, shouldRefreshLibrary } = await import("../../utils/metadataEdit");
const { parseFilename } = await import("../../utils/parseFilename");

import type { EditableField } from "../../utils/metadataEdit";
import type { MediaDetailResponse } from "~~/generated/rivet/types/models";
import type { SubtitleTrack } from "~~/generated/rivet/types/domain";

const formatDuration = (seconds: number): string => {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
};

/**
 * Replicates the detail page's core logic without mounting the Vue component.
 * Tests state transitions and API wiring.
 */
const setupDetailPage = (routePath: string | string[]) => {
  // Simulate route params decoding
  const relativePath = Array.isArray(routePath) ? routePath.join("/") : routePath;
  const fileName = relativePath.split("/").pop() ?? "";
  const parsed = fileName ? parseFilename(fileName) : null;

  // Data state
  const detail = ref<MediaDetailResponse | null>(null);
  const detailLoading = ref(false);
  const subtitles = ref<SubtitleTrack[]>([]);

  const { casting, error: castError, cast } = useCast();
  const { loading: watchLoading, setWatched, setUnwatched } = useWatchState();
  const { tags, loading: tagsLoading, getTags, addTag, removeTag } = useTags();
  const newTag = ref("");

  const isWatched = computed(() => detail.value?.watched === true);
  const displayTitle = computed(() => detail.value?.displayTitle ?? fileName);

  // Thumbnail src (mirrors page logic)
  const thumbnailSrc = computed(() =>
    relativePath ? mockThumbnailUrl(relativePath, "test-key") : null,
  );

  // Hero image fallback chain
  const backdropFailed = ref(false);
  const thumbnailFailed = ref(false);
  const posterFailed = ref(false);
  const posterInsetFailed = ref(false);

  type HeroMode = "backdrop" | "thumbnail" | "poster" | "icon";

  const heroMode = computed<HeroMode>(() => {
    if (detail.value?.backdropUrl && !backdropFailed.value) { return "backdrop"; }
    if (thumbnailSrc.value && !thumbnailFailed.value) { return "thumbnail"; }
    if (detail.value?.posterUrl && !posterFailed.value) { return "poster"; }
    return "icon";
  });

  const heroSrc = computed<string | null>(() => {
    if (heroMode.value === "backdrop") { return detail.value!.backdropUrl; }
    if (heroMode.value === "thumbnail") { return thumbnailSrc.value; }
    if (heroMode.value === "poster") { return detail.value!.posterUrl; }
    return null;
  });

  const showPosterInset = computed(() =>
    heroMode.value === "backdrop" && !!detail.value?.posterUrl && !posterInsetFailed.value,
  );

  const formattedDuration = computed(() => {
    const dur = detail.value?.durationSeconds;
    if (!dur || dur <= 0) { return null; }
    return formatDuration(dur);
  });

  const progressPercent = computed(() => {
    const pos = detail.value?.positionSeconds;
    const dur = detail.value?.durationSeconds;
    if (!pos || pos <= 0 || !dur || dur <= 0) { return null; }
    return (pos / dur) * 100;
  });

  const remainingTime = computed(() => {
    const pos = detail.value?.positionSeconds;
    const dur = detail.value?.durationSeconds;
    if (!pos || pos <= 0 || !dur || dur <= 0) { return null; }
    const remaining = dur - pos;
    if (remaining <= 0) { return null; }
    return `${formatDuration(remaining)} remaining`;
  });

  const formattedReleaseDate = computed(() => {
    const rd = detail.value?.releaseDate;
    if (!rd) { return null; }
    const date = new Date(rd);
    if (isNaN(date.getTime())) { return null; }
    return `Released ${date.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}`;
  });

  // Genre NOT in editable fields — regression guard target
  const editableFields: { key: EditableField; label: string }[] = [
    { key: "displayTitle", label: "Title" },
    { key: "rating", label: "Rating" },
    { key: "language", label: "Language" },
  ];

  const editing = ref<EditableField | null>(null);
  const editValue = ref("");
  const saving = ref(false);

  const startEdit = (field: EditableField) => {
    editing.value = field;
    editValue.value = (detail.value?.[field] as string | null) ?? "";
  };

  const cancelEdit = () => {
    editing.value = null;
    editValue.value = "";
  };

  const saveEdit = async () => {
    if (!editing.value) { return; }
    const field = editing.value;
    const newVal = editValue.value.trim();
    const oldVal = (detail.value?.[field] as string | null) ?? "";
    if (newVal === oldVal) { cancelEdit(); return; }

    saving.value = true;
    try {
      await mockRivetFetch("PATCH", "/api/media/detail", {
        body: buildPatchBody(field, newVal),
        query: { path: relativePath },
      });
      detail.value = await mockMediaDetail(relativePath);
      if (shouldRefreshLibrary(field)) {
        await mockRefresh();
      }
      const toast = useToast();
      toast.add({ title: "Saved", icon: "i-lucide-check" });
    } finally {
      saving.value = false;
      cancelEdit();
    }
  };

  const saveNotes = async () => {
    const newVal = editValue.value.trim();
    const oldVal = detail.value?.notes ?? "";
    if (newVal === oldVal) { editing.value = null; editValue.value = ""; return; }

    saving.value = true;
    try {
      await mockRivetFetch("PATCH", "/api/media/detail", {
        body: buildPatchBody("notes", newVal),
        query: { path: relativePath },
      });
      detail.value = await mockMediaDetail(relativePath);
      const toast = useToast();
      toast.add({ title: "Saved", icon: "i-lucide-check" });
    } finally {
      saving.value = false;
      editing.value = null;
      editValue.value = "";
    }
  };

  const handleAddTag = async () => {
    const trimmed = newTag.value.trim();
    if (!trimmed) { return; }
    newTag.value = "";
    await addTag(relativePath, trimmed);
  };

  const handleRemoveTag = async (tag: string) => {
    await removeTag(relativePath, tag);
  };

  const handleToggleWatched = async () => {
    if (isWatched.value) {
      await setUnwatched(relativePath);
    } else {
      await setWatched(relativePath);
    }
    detail.value = await mockMediaDetail(relativePath);
  };

  const handleCast = async () => {
    await cast(relativePath);
  };

  // Simulate onMounted data loading
  const loadData = async () => {
    if (!relativePath) { return; }
    detailLoading.value = true;
    try {
      const [detailResult, , subtitlesResult] = await Promise.all([
        mockMediaDetail(relativePath),
        getTags(relativePath),
        mockSubtitlesList(relativePath).catch(() => [] as SubtitleTrack[]),
      ]);
      detail.value = detailResult;
      subtitles.value = subtitlesResult;
    } catch {
      // Detail fetch failed
    } finally {
      detailLoading.value = false;
    }
  };

  return {
    relativePath,
    fileName,
    parsed,
    detail,
    detailLoading,
    subtitles,
    casting,
    castError,
    watchLoading,
    tags,
    tagsLoading,
    newTag,
    isWatched,
    displayTitle,
    editableFields,
    editing,
    editValue,
    saving,
    startEdit,
    cancelEdit,
    saveEdit,
    saveNotes,
    handleAddTag,
    handleRemoveTag,
    handleToggleWatched,
    handleCast,
    loadData,
    // TMDB imagery & metadata
    heroMode,
    heroSrc,
    showPosterInset,
    formattedDuration,
    progressPercent,
    remainingTime,
    formattedReleaseDate,
    backdropFailed,
    thumbnailFailed,
    posterFailed,
    posterInsetFailed,
  };
};

const makeDetail = (overrides: Partial<MediaDetailResponse> = {}): MediaDetailResponse => ({
  relativePath: "Movies/Film.mkv",
  parsedTitle: "Film",
  year: 2023,
  genre: null,
  rating: "PG-13",
  language: "English",
  notes: null,
  displayTitle: "The Film",
  tmdbId: null,
  posterUrl: null,
  backdropUrl: null,
  overview: "A great film.",
  releaseDate: null,
  addedAt: "2024-01-01T00:00:00Z",
  updatedAt: "2024-01-01T00:00:00Z",
  watched: false,
  positionSeconds: null,
  durationSeconds: null,
  tags: [],
  ...overrides,
});

describe("Detail page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockMediaDetail.mockResolvedValue(makeDetail());
    mockSubtitlesList.mockResolvedValue([]);
    mockTagsList.mockResolvedValue([]);
    mockRivetFetch.mockResolvedValue(undefined);
    mockRefresh.mockResolvedValue(undefined);
    mockCast.mockResolvedValue(undefined);
    mockThumbnailUrl.mockReturnValue("http://localhost/thumb");
  });

  describe("path decoding from route params", () => {
    it("joins string array with slashes", () => {
      const { relativePath } = setupDetailPage(["Movies", "Harry Potter", "file.mkv"]);
      expect(relativePath).toBe("Movies/Harry Potter/file.mkv");
    });

    it("passes single string through unchanged", () => {
      const { relativePath } = setupDetailPage("Movies/file.mkv");
      expect(relativePath).toBe("Movies/file.mkv");
    });

    it("extracts filename from the last path segment", () => {
      const { fileName } = setupDetailPage(["Movies", "Sub Dir", "awesome.mkv"]);
      expect(fileName).toBe("awesome.mkv");
    });
  });

  describe("genre is not an editable field", () => {
    it("editableFields does NOT include genre", () => {
      const { editableFields } = setupDetailPage("Movies/Film.mkv");
      const keys = editableFields.map((f) => f.key);
      expect(keys).not.toContain("genre");
    });

    it("editableFields includes displayTitle, rating, language", () => {
      const { editableFields } = setupDetailPage("Movies/Film.mkv");
      const keys = editableFields.map((f) => f.key);
      expect(keys).toEqual(["displayTitle", "rating", "language"]);
    });
  });

  describe("data loading on mount", () => {
    it("fetches mediaDetail, tags, and subtitles in parallel with correct path", async () => {
      const subtitleTracks: SubtitleTrack[] = [
        { index: 0, label: "English", language: "eng", format: "SRT", source: "embedded", externalPath: null },
      ];
      mockSubtitlesList.mockResolvedValue(subtitleTracks);
      mockTagsList.mockResolvedValue(["action", "sci-fi"]);

      const page = setupDetailPage(["Movies", "Film.mkv"]);
      await page.loadData();

      expect(mockMediaDetail).toHaveBeenCalledWith("Movies/Film.mkv");
      expect(mockTagsList).toHaveBeenCalledWith("Movies/Film.mkv");
      expect(mockSubtitlesList).toHaveBeenCalledWith("Movies/Film.mkv");

      expect(page.detail.value).toEqual(makeDetail());
      expect(page.subtitles.value).toEqual(subtitleTracks);
      expect(page.tags.value).toEqual(["action", "sci-fi"]);
    });

    it("sets detailLoading true during fetch and false after", async () => {
      let resolveDetail: (v: MediaDetailResponse) => void;
      mockMediaDetail.mockReturnValue(
        new Promise<MediaDetailResponse>((resolve) => { resolveDetail = resolve; }),
      );

      const page = setupDetailPage("Movies/Film.mkv");
      const promise = page.loadData();

      expect(page.detailLoading.value).toBe(true);

      resolveDetail!(makeDetail());
      await promise;

      expect(page.detailLoading.value).toBe(false);
    });

    it("handles subtitles fetch failure gracefully", async () => {
      mockSubtitlesList.mockRejectedValue(new Error("No subtitles"));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.subtitles.value).toEqual([]);
      expect(page.detail.value).toEqual(makeDetail());
    });
  });

  describe("metadata inline editing", () => {
    it("saveEdit sends PATCH with buildPatchBody for displayTitle", async () => {
      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      // After load, change the mock return for the re-fetch after PATCH
      mockMediaDetail.mockResolvedValue(makeDetail({ displayTitle: "New Title" }));

      page.startEdit("displayTitle");
      page.editValue.value = "New Title";
      await page.saveEdit();

      expect(mockRivetFetch).toHaveBeenCalledWith("PATCH", "/api/media/detail", {
        body: {
          genre: null,
          rating: null,
          language: null,
          notes: null,
          displayTitle: "New Title",
        },
        query: { path: "Movies/Film.mkv" },
      });
    });

    it("saveEdit refreshes library when displayTitle changes", async () => {
      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      mockMediaDetail.mockResolvedValue(makeDetail({ displayTitle: "New Title" }));

      page.startEdit("displayTitle");
      page.editValue.value = "New Title";
      await page.saveEdit();

      expect(mockRefresh).toHaveBeenCalled();
    });

    it("saveEdit does NOT refresh library when rating changes", async () => {
      // Load with default rating "PG-13", then change to "R"
      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      // Re-mock for the re-fetch after PATCH
      mockMediaDetail.mockResolvedValue(makeDetail({ rating: "R" }));

      page.startEdit("rating");       // editValue = "PG-13" (from loaded detail)
      page.editValue.value = "R";     // change to different value
      await page.saveEdit();

      // PATCH was sent (proves we didn't take the early-return path)
      expect(mockRivetFetch).toHaveBeenCalledWith("PATCH", "/api/media/detail", {
        body: {
          genre: null,
          rating: "R",
          language: null,
          notes: null,
          displayTitle: null,
        },
        query: { path: "Movies/Film.mkv" },
      });
      // But refresh was NOT called — shouldRefreshLibrary("rating") is false
      expect(mockRefresh).not.toHaveBeenCalled();
    });

    it("saveEdit skips API call when value unchanged", async () => {
      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      // detail.displayTitle is "The Film"
      page.startEdit("displayTitle");
      page.editValue.value = "The Film";
      await page.saveEdit();

      expect(mockRivetFetch).not.toHaveBeenCalled();
      expect(page.editing.value).toBeNull();
    });

    it("saveNotes sends PATCH with notes field", async () => {
      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      // After load, notes is null. Update mock for re-fetch after PATCH.
      mockMediaDetail.mockResolvedValue(makeDetail({ notes: "Great movie" }));

      page.startEdit("notes");
      page.editValue.value = "Great movie";

      // notes was null, new value is "Great movie"
      await page.saveNotes();

      expect(mockRivetFetch).toHaveBeenCalledWith("PATCH", "/api/media/detail", {
        body: {
          genre: null,
          rating: null,
          language: null,
          notes: "Great movie",
          displayTitle: null,
        },
        query: { path: "Movies/Film.mkv" },
      });
    });
  });

  describe("tags add/remove", () => {
    it("handleAddTag delegates to composable addTag with correct path", async () => {
      mockTagsList.mockResolvedValue(["action"]);

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      page.newTag.value = "comedy";
      await page.handleAddTag();

      expect(mockRivetFetch).toHaveBeenCalledWith("POST", "/api/media/tags", {
        body: { tag: "comedy" },
        query: { path: "Movies/Film.mkv" },
      });
      expect(page.newTag.value).toBe("");
    });

    it("handleAddTag does nothing for empty input", async () => {
      const page = setupDetailPage("Movies/Film.mkv");
      page.newTag.value = "   ";
      await page.handleAddTag();

      expect(mockRivetFetch).not.toHaveBeenCalled();
    });

    it("handleRemoveTag delegates to composable removeTag", async () => {
      mockTagsList.mockResolvedValue(["action", "comedy"]);
      mockTagsRemove.mockResolvedValue(undefined);

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      await page.handleRemoveTag("action");

      expect(mockTagsRemove).toHaveBeenCalledWith("Movies/Film.mkv", "action");
    });
  });

  describe("watched toggle", () => {
    it("calls setWatched when currently unwatched", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ watched: false }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.isWatched.value).toBe(false);

      await page.handleToggleWatched();

      expect(mockRivetFetch).toHaveBeenCalledWith("PUT", "/api/media/watched", {
        body: { path: "Movies/Film.mkv" },
        query: { path: "Movies/Film.mkv" },
      });
    });

    it("calls setUnwatched when currently watched", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ watched: true }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.isWatched.value).toBe(true);

      await page.handleToggleWatched();

      expect(mockRivetFetch).toHaveBeenCalledWith("PUT", "/api/media/unwatched", {
        body: { path: "Movies/Film.mkv" },
        query: { path: "Movies/Film.mkv" },
      });
    });

    it("re-fetches detail after toggle to update watched state", async () => {
      mockMediaDetail
        .mockResolvedValueOnce(makeDetail({ watched: false })) // initial load
        .mockResolvedValueOnce(makeDetail({ watched: true })); // after toggle

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.isWatched.value).toBe(false);

      await page.handleToggleWatched();

      // mediaDetail called twice: once for load, once after toggle
      expect(mockMediaDetail).toHaveBeenCalledTimes(2);
      expect(page.isWatched.value).toBe(true);
    });
  });

  describe("cast", () => {
    it("delegates to useCast composable with correct path", async () => {
      const page = setupDetailPage("Movies/Film.mkv");
      await page.handleCast();

      expect(mockCast).toHaveBeenCalledWith({ relativePath: "Movies/Film.mkv" });
    });
  });

  describe("display title", () => {
    it("uses detail displayTitle when available", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ displayTitle: "Custom Title" }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.displayTitle.value).toBe("Custom Title");
    });

    it("falls back to filename when no displayTitle", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ displayTitle: null }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.displayTitle.value).toBe("Film.mkv");
    });
  });

  describe("hero image fallback chain", () => {
    it("uses backdropUrl as hero when available", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ backdropUrl: "https://tmdb.org/backdrop.jpg" }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.heroMode.value).toBe("backdrop");
      expect(page.heroSrc.value).toBe("https://tmdb.org/backdrop.jpg");
    });

    it("falls back to thumbnail when no backdrop", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ backdropUrl: null }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.heroMode.value).toBe("thumbnail");
      expect(page.heroSrc.value).toBe("http://localhost/thumb");
    });

    it("falls back to poster when no backdrop and thumbnail fails", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({
        backdropUrl: null,
        posterUrl: "https://tmdb.org/poster.jpg",
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      // Simulate thumbnail image error
      page.thumbnailFailed.value = true;

      expect(page.heroMode.value).toBe("poster");
      expect(page.heroSrc.value).toBe("https://tmdb.org/poster.jpg");
    });

    it("falls back to icon when nothing available", async () => {
      mockThumbnailUrl.mockReturnValue(null);
      mockMediaDetail.mockResolvedValue(makeDetail({
        backdropUrl: null,
        posterUrl: null,
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.heroMode.value).toBe("icon");
      expect(page.heroSrc.value).toBeNull();
    });

    it("falls through backdrop to thumbnail when backdrop fails", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ backdropUrl: "https://tmdb.org/backdrop.jpg" }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.heroMode.value).toBe("backdrop");

      // Simulate backdrop @error
      page.backdropFailed.value = true;

      expect(page.heroMode.value).toBe("thumbnail");
      expect(page.heroSrc.value).toBe("http://localhost/thumb");
    });
  });

  describe("poster inset overlay", () => {
    it("shows poster inset when backdrop and poster both exist", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({
        backdropUrl: "https://tmdb.org/backdrop.jpg",
        posterUrl: "https://tmdb.org/poster.jpg",
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.showPosterInset.value).toBe(true);
    });

    it("hides poster inset when only backdrop (no poster)", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({
        backdropUrl: "https://tmdb.org/backdrop.jpg",
        posterUrl: null,
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.showPosterInset.value).toBe(false);
    });

    it("hides poster inset when only poster (no backdrop — heroMode is not backdrop)", async () => {
      mockThumbnailUrl.mockReturnValue(null);
      mockMediaDetail.mockResolvedValue(makeDetail({
        backdropUrl: null,
        posterUrl: "https://tmdb.org/poster.jpg",
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.heroMode.value).not.toBe("backdrop");
      expect(page.showPosterInset.value).toBe(false);
    });

    it("hides poster inset when poster inset image fails to load", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({
        backdropUrl: "https://tmdb.org/backdrop.jpg",
        posterUrl: "https://tmdb.org/poster.jpg",
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.showPosterInset.value).toBe(true);

      page.posterInsetFailed.value = true;

      expect(page.showPosterInset.value).toBe(false);
    });
  });

  describe("duration formatting", () => {
    it("formats hours and minutes", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ durationSeconds: 7620 }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.formattedDuration.value).toBe("2h 7m");
    });

    it("formats minutes only when under an hour", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ durationSeconds: 2520 }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.formattedDuration.value).toBe("42m");
    });

    it("returns null when durationSeconds is null", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ durationSeconds: null }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.formattedDuration.value).toBeNull();
    });

    it("returns null when durationSeconds is 0", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ durationSeconds: 0 }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.formattedDuration.value).toBeNull();
    });
  });

  describe("progress bar and remaining time", () => {
    it("computes progress percent from position and duration", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({
        positionSeconds: 1800,
        durationSeconds: 7200,
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.progressPercent.value).toBe(25);
    });

    it("computes remaining time formatted", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({
        positionSeconds: 1800,
        durationSeconds: 7200,
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.remainingTime.value).toBe("1h 30m remaining");
    });

    it("returns null progress when positionSeconds is null", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({
        positionSeconds: null,
        durationSeconds: 7200,
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.progressPercent.value).toBeNull();
      expect(page.remainingTime.value).toBeNull();
    });

    it("returns null progress when positionSeconds is 0", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({
        positionSeconds: 0,
        durationSeconds: 7200,
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.progressPercent.value).toBeNull();
    });

    it("returns null when durationSeconds is 0", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({
        positionSeconds: 100,
        durationSeconds: 0,
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.progressPercent.value).toBeNull();
    });

    it("handles nearly complete progress", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({
        positionSeconds: 7100,
        durationSeconds: 7200,
      }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.progressPercent.value).toBeCloseTo(98.6, 0);
      expect(page.remainingTime.value).toBe("1m remaining");
    });
  });

  describe("release date", () => {
    it("formats release date when available", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ releaseDate: "2009-03-15" }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.formattedReleaseDate.value).toBe("Released Mar 15, 2009");
    });

    it("returns null when releaseDate is null", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ releaseDate: null }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.formattedReleaseDate.value).toBeNull();
    });

    it("returns null for invalid date string", async () => {
      mockMediaDetail.mockResolvedValue(makeDetail({ releaseDate: "not-a-date" }));

      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      expect(page.formattedReleaseDate.value).toBeNull();
    });
  });

  describe("toast notifications", () => {
    it("shows toast after saveEdit succeeds", async () => {
      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      mockMediaDetail.mockResolvedValue(makeDetail({ displayTitle: "New Title" }));

      page.startEdit("displayTitle");
      page.editValue.value = "New Title";
      await page.saveEdit();

      expect(mockToastAdd).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "Saved",
          icon: "i-lucide-check",
        }),
      );
    });

    it("shows toast after saveNotes succeeds", async () => {
      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      mockMediaDetail.mockResolvedValue(makeDetail({ notes: "Great movie" }));

      page.startEdit("notes");
      page.editValue.value = "Great movie";
      await page.saveNotes();

      expect(mockToastAdd).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "Saved",
          icon: "i-lucide-check",
        }),
      );
    });

    it("does not show toast when saveEdit is a no-op (value unchanged)", async () => {
      const page = setupDetailPage("Movies/Film.mkv");
      await page.loadData();

      page.startEdit("displayTitle");
      page.editValue.value = "The Film"; // same as loaded value
      await page.saveEdit();

      expect(mockToastAdd).not.toHaveBeenCalled();
    });
  });
});
