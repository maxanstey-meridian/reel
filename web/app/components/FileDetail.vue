<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useCast } from "~~/app/composables/useCast";
import { useTags } from "~~/app/composables/useTags";
import { useWatchState } from "~~/app/composables/useWatchState";
import { buildPatchBody, shouldRefreshLibrary } from "~~/app/utils/metadataEdit";
import type { EditableField } from "~~/app/utils/metadataEdit";
import { parseFilename } from "~~/app/utils/parseFilename";
import { mediaDetail as fetchMediaDetail, subtitlesList } from "~~/generated/rivet/client/reel";
import { rivetFetch } from "~~/generated/rivet/rivet";
import type { FileNode, SubtitleTrack } from "~~/generated/rivet/types/domain";
import type { MediaDetailResponse } from "~~/generated/rivet/types/models";

const props = defineProps<{
  node: FileNode | null;
  open: boolean;
}>();

const emit = defineEmits<{ close: [] }>();

const { casting, error, cast } = useCast();
const { loading: watchLoading, setWatched, setUnwatched } = useWatchState();
const { tags, loading: tagsLoading, getTags, addTag, removeTag } = useTags();
const newTag = ref("");

const isWatched = computed(() => props.node?.watched === true);

const editing = ref<EditableField | null>(null);
const editValue = ref("");
const saving = ref(false);

const editableFields: { key: EditableField; label: string }[] = [
  { key: "displayTitle", label: "Title" },
  { key: "genre", label: "Genre" },
  { key: "rating", label: "Rating" },
  { key: "language", label: "Language" },
];

const startEdit = (field: EditableField) => {
  editing.value = field;
  editValue.value = (detail.value?.[field] as string | null) ?? "";
};

const cancelEdit = () => {
  editing.value = null;
  editValue.value = "";
};

const saveEdit = async () => {
  if (!editing.value || !props.node) {
    return;
  }
  const field = editing.value;
  const newVal = editValue.value.trim();
  const oldVal = (detail.value?.[field] as string | null) ?? "";
  if (newVal === oldVal) {
    cancelEdit();
    return;
  }

  saving.value = true;
  try {
    // rivetFetch used directly because generated mediaUpdate() has no path param
    // but server reads path from [FromQuery]
    await rivetFetch("PATCH", "/api/media/detail", {
      body: buildPatchBody(field, newVal),
      query: { path: props.node.relativePath },
    });

    detail.value = await fetchMediaDetail(props.node.relativePath);

    if (shouldRefreshLibrary(field)) {
      await useLibrary().refresh();
    }
  } finally {
    saving.value = false;
    cancelEdit();
  }
};

const saveNotes = async () => {
  if (!props.node) {
    return;
  }
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
      query: { path: props.node.relativePath },
    });
    detail.value = await fetchMediaDetail(props.node.relativePath);
  } finally {
    saving.value = false;
    editing.value = null;
    editValue.value = "";
  }
};

const handleAddTag = async () => {
  const trimmed = newTag.value.trim();
  if (!trimmed || !props.node) {
    return;
  }
  newTag.value = "";
  await addTag(props.node.relativePath, trimmed);
};

const handleRemoveTag = async (tag: string) => {
  if (!props.node) {
    return;
  }
  await removeTag(props.node.relativePath, tag);
};

const handleToggleWatched = async () => {
  if (!props.node) {
    return;
  }
  if (isWatched.value) {
    await setUnwatched(props.node.relativePath);
  } else {
    await setWatched(props.node.relativePath);
  }
};

const parsed = computed(() => (props.node ? parseFilename(props.node.name) : null));

const detail = ref<MediaDetailResponse | null>(null);
const detailLoading = ref(false);

const fetchDetail = async (node: FileNode) => {
  detail.value = null;
  detailLoading.value = true;
  try {
    detail.value = await fetchMediaDetail(node.relativePath);
  } catch {
    // Detail is optional — silently ignore
  } finally {
    detailLoading.value = false;
  }
};

const subtitles = ref<SubtitleTrack[]>([]);

const fetchSubtitles = async (path: string) => {
  try {
    subtitles.value = await subtitlesList(path);
  } catch {
    subtitles.value = [];
  }
};

watch(
  () => [props.node, props.open] as const,
  ([node, open]) => {
    if (open && node) {
      fetchDetail(node);
      getTags(node.relativePath);
      fetchSubtitles(node.relativePath);
    }
    if (!open) {
      detail.value = null;
      tags.value = [];
      subtitles.value = [];
      cancelEdit();
    }
  },
);

const formatSize = (bytes: number | null): string => {
  if (bytes === null) {
    return "";
  }
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(0)} KB`;
  }
  if (bytes < 1024 * 1024 * 1024) {
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
};

const handleCast = async () => {
  if (!props.node) {
    return;
  }
  await cast(props.node.relativePath);
  if (!error.value) {
    emit("close");
  }
};
</script>

<template>
  <USlideover
    :open="open"
    side="bottom"
    :ui="{ content: 'bg-neutral-950 text-white', header: 'bg-neutral-950', body: 'bg-neutral-950' }"
    @update:open="
      (val: boolean) => {
        if (!val) $emit('close');
      }
    "
  >
    <template v-if="node" #header>
      <div class="flex items-center gap-3 w-full">
        <div class="flex-1 min-w-0">
          <h2 class="text-base font-semibold truncate">{{ node.displayTitle ?? node.name }}</h2>
          <div class="flex items-center gap-2 mt-0.5">
            <UBadge v-if="node.extension" size="xs" color="neutral" variant="subtle">
              {{ node.extension }}
            </UBadge>
            <span v-if="node.sizeBytes" class="text-xs text-neutral-500">
              {{ formatSize(node.sizeBytes) }}
            </span>
            <span v-if="parsed?.year" class="text-xs text-neutral-500">
              {{ parsed.year }}
            </span>
          </div>
        </div>
      </div>
    </template>

    <template #body>
      <div class="px-6 pb-6 space-y-3">
        <p v-if="detail?.overview" class="text-sm text-neutral-400 leading-relaxed">
          {{ detail.overview }}
        </p>

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

        <UAlert v-if="error" color="error" icon="i-lucide-alert-circle" :title="error" />

        <UButton block size="lg" icon="i-lucide-cast" :loading="casting" @click="handleCast">
          Cast to TV
        </UButton>
      </div>
    </template>
  </USlideover>
</template>
