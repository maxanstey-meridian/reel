<script setup lang="ts">
import { computed, ref } from "vue";
import { useCollections } from "~~/app/composables/useCollections";
import { useConnection } from "~~/app/composables/useConnection";
import { useCast } from "~~/app/composables/useCast";
import { useTags } from "~~/app/composables/useTags";
import { useWatchState } from "~~/app/composables/useWatchState";
import { buildPatchBody, shouldRefreshLibrary } from "~~/app/utils/metadataEdit";
import type { EditableField } from "~~/app/utils/metadataEdit";
import { parseFilename } from "~~/app/utils/parseFilename";
import { mediaDetail as fetchMediaDetail, subtitlesList, thumbnailUrl } from "~~/generated/rivet/client/reel";
import { rivetFetch } from "~~/generated/rivet/rivet";
import type { SubtitleTrack } from "~~/generated/rivet/types/domain";
import type { MediaDetailResponse } from "~~/generated/rivet/types/models";

const formatDuration = (seconds: number): string => {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
};

const route = useRoute();
const router = useRouter();
const { secretKey } = useConnection();

// Decode path from catch-all route params (string[] or string)
const relativePath = computed(() => {
  const p = route.params.path;
  if (Array.isArray(p)) {
    return p.join("/");
  }
  return p ?? "";
});

const fileName = computed(() => {
  const parts = relativePath.value.split("/");
  return parts[parts.length - 1] ?? "";
});

const parsed = computed(() => (fileName.value ? parseFilename(fileName.value) : null));

// Data state
const detail = ref<MediaDetailResponse | null>(null);
const detailLoading = ref(false);
const subtitles = ref<SubtitleTrack[]>([]);

const { casting, error: castError, cast } = useCast();
const { loading: watchLoading, setWatched, setUnwatched } = useWatchState();
const { tags, loading: tagsLoading, getTags, addTag, removeTag } = useTags();
const { collections, fetch: fetchCollections, addItem: addToCollection, create: createCollection } = useCollections();
const newTag = ref("");

// Collection popover
const collectionPopoverOpen = ref(false);
const newCollectionName = ref("");
const creatingInCollection = ref(false);

const openCollectionPopover = async () => {
  if (collections.value.length === 0) {
    await fetchCollections();
  }
  collectionPopoverOpen.value = true;
};

const handleAddToCollection = async (collectionId: number) => {
  await addToCollection(collectionId, relativePath.value);
  collectionPopoverOpen.value = false;
};

const handleCreateAndAddCollection = async () => {
  const name = newCollectionName.value.trim();
  if (!name) return;
  creatingInCollection.value = true;
  try {
    const created = await createCollection(name);
    if (created) {
      await addToCollection(created.id, relativePath.value);
    }
    newCollectionName.value = "";
    collectionPopoverOpen.value = false;
  } finally {
    creatingInCollection.value = false;
  }
};

const isWatched = computed(() => detail.value?.watched === true);

const displayTitle = computed(() => detail.value?.displayTitle ?? fileName.value);

const thumbnailSrc = computed(() =>
  relativePath.value && secretKey.value
    ? thumbnailUrl(relativePath.value, secretKey.value)
    : null,
);

// Hero image fallback chain: backdrop → thumbnail → poster → icon
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

// Duration & progress
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

// Release date
const formattedReleaseDate = computed(() => {
  const rd = detail.value?.releaseDate;
  if (!rd) { return null; }
  const date = new Date(rd);
  if (isNaN(date.getTime())) { return null; }
  return `Released ${date.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}`;
});

// Editable metadata — genre is NOT included (managed through tags)
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
  editValue.value = detail.value?.[field] ?? "";
};

const cancelEdit = () => {
  editing.value = null;
  editValue.value = "";
};

const saveEdit = async () => {
  if (!editing.value) {
    return;
  }
  const field = editing.value;
  const newVal = editValue.value.trim();
  const oldVal = detail.value?.[field] ?? "";
  if (newVal === oldVal) {
    cancelEdit();
    return;
  }

  saving.value = true;
  try {
    await rivetFetch("PATCH", "/api/media/detail", {
      body: buildPatchBody(field, newVal),
      query: { path: relativePath.value },
    });

    detail.value = await fetchMediaDetail(relativePath.value);

    if (shouldRefreshLibrary(field)) {
      await useLibrary().refresh();
    }
  } finally {
    saving.value = false;
    cancelEdit();
  }
};

const saveNotes = async () => {
  const newVal = editValue.value.trim();
  const oldVal = detail.value?.notes ?? "";
  if (newVal === oldVal) {
    editing.value = null;
    editValue.value = "";
    return;
  }

  saving.value = true;
  try {
    await rivetFetch("PATCH", "/api/media/detail", {
      body: buildPatchBody("notes", newVal),
      query: { path: relativePath.value },
    });
    detail.value = await fetchMediaDetail(relativePath.value);
  } finally {
    saving.value = false;
    editing.value = null;
    editValue.value = "";
  }
};

const handleAddTag = async () => {
  const trimmed = newTag.value.trim();
  if (!trimmed) {
    return;
  }
  newTag.value = "";
  await addTag(relativePath.value, trimmed);
};

const handleRemoveTag = async (tag: string) => {
  await removeTag(relativePath.value, tag);
};

const handleToggleWatched = async () => {
  if (isWatched.value) {
    await setUnwatched(relativePath.value);
  } else {
    await setWatched(relativePath.value);
  }
  // Re-fetch detail to update watched state
  detail.value = await fetchMediaDetail(relativePath.value);
};

const handleCast = async () => {
  await cast(relativePath.value);
};

// Load data on mount
onMounted(async () => {
  const path = relativePath.value;
  if (!path) {
    return;
  }

  detailLoading.value = true;
  try {
    const [detailResult, , subtitlesResult] = await Promise.all([
      fetchMediaDetail(path),
      getTags(path),
      subtitlesList(path).catch(() => [] as SubtitleTrack[]),
    ]);
    detail.value = detailResult;
    subtitles.value = subtitlesResult;
  } catch {
    // Detail fetch failed — page will show minimal info from path
  } finally {
    detailLoading.value = false;
  }
});
</script>

<template>
  <div class="space-y-4">
    <!-- Header -->
    <div class="flex items-center gap-3">
      <UButton
        variant="ghost"
        color="neutral"
        size="sm"
        icon="i-lucide-arrow-left"
        @click="router.back()"
      />
      <h1 class="text-base font-semibold truncate flex-1">
        {{ displayTitle }}
      </h1>
    </div>

    <!-- Loading -->
    <div v-if="detailLoading" class="space-y-3">
      <USkeleton class="h-56 w-full rounded" />
      <USkeleton class="h-4 w-3/4" />
      <USkeleton class="h-4 w-1/2" />
    </div>

    <template v-else>
      <!-- Hero image -->
      <div class="relative rounded overflow-hidden bg-neutral-800/50">
        <!-- Backdrop or thumbnail: full-width, object-cover -->
        <img
          v-if="heroMode === 'backdrop'"
          :src="heroSrc ?? ''"
          class="w-full h-56 object-cover"
          @error="backdropFailed = true"
        />
        <img
          v-else-if="heroMode === 'thumbnail'"
          :src="heroSrc ?? ''"
          class="w-full h-56 object-cover"
          @error="thumbnailFailed = true"
        />
        <!-- Poster only: centered portrait image -->
        <div v-else-if="heroMode === 'poster'" class="h-56 flex items-center justify-center">
          <img
            :src="heroSrc ?? ''"
            class="max-h-full w-auto max-w-[140px] rounded"
            @error="posterFailed = true"
          />
        </div>
        <!-- Fallback icon -->
        <div v-else class="h-56 flex items-center justify-center text-neutral-500">
          <UIcon name="i-lucide-film" class="size-16" />
        </div>

        <!-- Poster inset overlay -->
        <img
          v-if="showPosterInset"
          :src="detail!.posterUrl!"
          class="absolute bottom-3 left-3 w-[60px] rounded-md shadow-lg"
          @error="posterInsetFailed = true"
        />

        <!-- Overlaid badges -->
        <div class="absolute bottom-2 left-2 flex items-center gap-1.5" :class="{ 'left-20': showPosterInset }">
          <UBadge v-if="formattedReleaseDate" size="xs" color="neutral" variant="subtle">
            {{ formattedReleaseDate }}
          </UBadge>
          <UBadge v-else-if="parsed?.year || detail?.year" size="xs" color="neutral" variant="subtle">
            {{ detail?.year ?? parsed?.year }}
          </UBadge>
        </div>
      </div>

      <!-- Progress bar -->
      <div v-if="progressPercent != null && progressPercent > 0" class="space-y-1">
        <div class="h-1 bg-neutral-700 rounded-full overflow-hidden">
          <div class="h-full bg-green-500" :style="{ width: `${Math.min(progressPercent, 100)}%` }" />
        </div>
        <p v-if="remainingTime" class="text-xs text-neutral-500">{{ remainingTime }}</p>
      </div>

      <!-- Extension + size row -->
      <div class="flex items-center gap-2">
        <UBadge v-if="fileName.split('.').pop()" size="xs" color="neutral" variant="subtle">
          .{{ fileName.split('.').pop() }}
        </UBadge>
        <UBadge v-if="formattedDuration" size="xs" color="neutral" variant="subtle">
          {{ formattedDuration }}
        </UBadge>
        <span class="text-xs text-neutral-500 truncate">
          {{ relativePath }}
        </span>
      </div>

      <!-- Overview -->
      <p v-if="detail?.overview" class="text-sm text-neutral-400 leading-relaxed">
        {{ detail.overview }}
      </p>

      <!-- Editable metadata fields -->
      <div v-if="detail" class="space-y-2">
        <div v-for="{ key, label } in editableFields" :key="key" class="flex items-center gap-2">
          <span class="text-xs text-neutral-500 w-16 shrink-0">{{ label }}</span>
          <template v-if="editing === key">
            <UInput
              v-model="editValue"
              size="xs"
              class="flex-1"
              :disabled="saving"
              @keydown.enter="saveEdit"
              @keydown.escape="cancelEdit"
              @blur="saveEdit"
            />
          </template>
          <template v-else>
            <button
              class="flex items-center gap-1 text-sm text-neutral-300 hover:text-white truncate"
              @click="startEdit(key)"
            >
              {{ detail[key] ?? '—' }}
              <span class="i-lucide-pencil size-3 text-neutral-500 shrink-0" />
            </button>
          </template>
        </div>

        <!-- Notes -->
        <div class="flex items-start gap-2">
          <span class="text-xs text-neutral-500 w-16 shrink-0 mt-1">Notes</span>
          <template v-if="editing === 'notes'">
            <div class="flex-1 space-y-1">
              <UTextarea
                v-model="editValue"
                size="xs"
                :rows="3"
                :disabled="saving"
                @keydown.escape="cancelEdit"
              />
              <div class="flex gap-1">
                <UButton size="xs" @click="saveNotes">Save</UButton>
                <UButton size="xs" variant="ghost" @click="cancelEdit">Cancel</UButton>
              </div>
            </div>
          </template>
          <template v-else>
            <button
              class="flex items-center gap-1 text-sm text-neutral-300 hover:text-white"
              @click="startEdit('notes')"
            >
              {{ detail.notes ?? '—' }}
              <span class="i-lucide-pencil size-3 text-neutral-500 shrink-0" />
            </button>
          </template>
        </div>
      </div>

      <!-- Tags -->
      <div class="flex flex-wrap items-center gap-1.5">
        <UBadge
          v-for="tag in tags"
          :key="tag"
          size="sm"
          color="neutral"
          variant="subtle"
          class="gap-1"
        >
          {{ tag }}
          <button
            class="i-lucide-x size-3 opacity-60 hover:opacity-100"
            :disabled="tagsLoading"
            @click="handleRemoveTag(tag)"
          />
        </UBadge>
        <UInput
          v-model="newTag"
          size="xs"
          placeholder="Add tag..."
          :disabled="tagsLoading"
          class="w-24"
          @keydown.enter="handleAddTag"
        />
      </div>

      <!-- Watched toggle -->
      <UButton
        block
        size="lg"
        variant="outline"
        :icon="isWatched ? 'i-lucide-eye-off' : 'i-lucide-eye'"
        :loading="watchLoading"
        @click="handleToggleWatched"
      >
        {{ isWatched ? "Mark Unwatched" : "Mark Watched" }}
      </UButton>

      <!-- Cast error -->
      <UAlert v-if="castError" color="error" icon="i-lucide-alert-circle" :title="castError" />

      <!-- Cast button -->
      <UButton block size="lg" icon="i-lucide-cast" :loading="casting" @click="handleCast">
        Cast to TV
      </UButton>

      <!-- Add to collection -->
      <UPopover v-model:open="collectionPopoverOpen">
        <UButton
          block
          size="lg"
          variant="outline"
          icon="i-lucide-list-plus"
          data-testid="detail-add-to-collection-btn"
          @click="openCollectionPopover"
        >
          Add to Collection
        </UButton>
        <template #content>
          <div class="p-2 w-56 space-y-1">
            <p class="text-xs text-neutral-500 px-1 mb-1">Add to collection</p>
            <button
              v-for="c in collections"
              :key="c.id"
              class="w-full text-left text-sm px-2 py-1.5 rounded hover:bg-neutral-800 text-neutral-300 hover:text-white transition-colors truncate"
              @click="handleAddToCollection(c.id)"
            >
              {{ c.name }}
            </button>
            <div v-if="collections.length === 0" class="text-xs text-neutral-500 px-2 py-1">
              No collections yet
            </div>
            <div class="border-t border-neutral-800 pt-1 mt-1">
              <form class="flex items-center gap-1" @submit.prevent="handleCreateAndAddCollection">
                <input
                  v-model="newCollectionName"
                  type="text"
                  placeholder="New collection..."
                  class="flex-1 text-xs bg-transparent border border-neutral-700 rounded px-2 py-1 text-neutral-300 placeholder-neutral-600 outline-none focus:border-green-500"
                  :disabled="creatingInCollection"
                />
                <button
                  type="submit"
                  class="text-xs text-green-500 hover:text-green-400 px-1 disabled:opacity-50"
                  :disabled="!newCollectionName.trim() || creatingInCollection"
                >
                  Add
                </button>
              </form>
            </div>
          </div>
        </template>
      </UPopover>

      <!-- Subtitles -->
      <div v-if="subtitles.length > 0" class="space-y-1.5">
        <span class="text-xs text-neutral-500">Subtitles</span>
        <div
          v-for="track in subtitles"
          :key="track.index"
          class="flex items-center gap-2 text-sm text-neutral-300"
        >
          <span class="truncate">{{ track.language || 'Unknown' }}</span>
          <UBadge size="xs" color="neutral" variant="subtle">{{ track.format }}</UBadge>
          <UBadge size="xs" color="neutral" variant="outline">{{ track.source }}</UBadge>
        </div>
      </div>
    </template>
  </div>
</template>
