import { ref } from "vue";
import { tagsList, tagsRemove, tagsAll } from "~~/generated/rivet/client/reel";
import { rivetFetch } from "~~/generated/rivet/rivet";
import type { TagCount } from "~~/generated/rivet/types/domain";

export type { TagCount };

export const useTags = () => {
  const tags = ref<string[]>([]);
  const loading = ref(false);

  const getTags = async (path: string) => {
    loading.value = true;
    try {
      tags.value = await tagsList(path);
    } finally {
      loading.value = false;
    }
  };

  // rivetFetch used directly because generated tagsAdd() only accepts { tag } body
  // but server reads path from [FromQuery] — codegen doesn't support mixed body+query
  const addTag = async (path: string, tag: string) => {
    loading.value = true;
    try {
      await rivetFetch("POST", "/api/media/tags", { body: { tag }, query: { path } });
      tags.value = await tagsList(path);
    } finally {
      loading.value = false;
    }
  };

  const removeTag = async (path: string, tag: string) => {
    loading.value = true;
    try {
      await tagsRemove(path, tag);
      tags.value = await tagsList(path);
    } finally {
      loading.value = false;
    }
  };

  const getAllTags = async (): Promise<TagCount[]> => {
    return await tagsAll();
  };

  return { tags, loading, getTags, addTag, removeTag, getAllTags };
};
