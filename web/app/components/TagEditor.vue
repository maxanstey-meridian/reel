<script setup lang="ts">
import type { FileNode } from "~~/app/lib/api";
import { injectTags } from "~~/app/ports/tags";

const props = defineProps<{ path: string; type: FileNode["type"] }>();
const { forPath, loaded, error: loadError, refresh, save } = injectTags();
const tags = computed(() => forPath(props.path, props.type));
const adding = ref(false);
const draft = ref("");
const busy = ref(false);
const locked = computed(() => busy.value || !loaded.value);
const error = ref("");
const retrying = ref(false);
const retry = async () => {
  retrying.value = true;
  await refresh().finally(() => {
    retrying.value = false;
  });
};

const update = async (values: string[]) => {
  busy.value = true;
  error.value = "";
  try {
    await save(props.path, values);
    draft.value = "";
    adding.value = false;
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : "Could not save tags";
  } finally {
    busy.value = false;
  }
};
</script>

<template>
  <div class="space-y-2">
    <div class="flex flex-wrap items-center gap-2">
      <UBadge v-for="tag in tags" :key="tag" color="neutral" variant="subtle" class="gap-1">
        #{{ tag }}
        <UButton
          :aria-label="`Delete tag ${tag}`"
          :disabled="locked"
          icon="i-lucide-x"
          color="neutral"
          variant="link"
          size="xs"
          class="p-0"
          @click="update(tags.filter((item) => item !== tag))"
        />
      </UBadge>
      <UButton
        :disabled="locked"
        icon="i-lucide-plus"
        color="neutral"
        variant="ghost"
        size="xs"
        @click="void (adding = !adding)"
      >
        Add tag
      </UButton>
    </div>
    <form v-if="adding" class="flex gap-2" @submit.prevent="update([...tags, draft.trim()])">
      <UInput
        v-model="draft"
        aria-label="New tag"
        placeholder="Tag name"
        maxlength="40"
        pattern="[^\s]+"
        size="sm"
        autofocus
      />
      <UButton type="submit" size="sm" :loading="busy" :disabled="!loaded || !draft.trim()">
        Add
      </UButton>
    </form>
    <p v-if="error" role="alert" class="text-xs text-red-400">{{ error }}</p>
    <p v-else-if="loadError" class="flex items-center gap-2 text-xs text-amber-400">
      Tags couldn't load
      <UButton
        variant="link"
        color="neutral"
        size="xs"
        class="p-0"
        :loading="retrying"
        @click="void retry()"
      >
        Try again
      </UButton>
    </p>
  </div>
</template>
