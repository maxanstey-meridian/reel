import { useProvideInject } from "~~/app/composables/useProvideInject";
import type { FileNode } from "~~/app/lib/api";

export interface Tags {
  forPath: (path: string, type: FileNode["type"]) => string[];
  /** A save replaces an item's whole set, so edits wait until the saved sets have loaded. */
  loaded: Readonly<Ref<boolean>>;
  error: Readonly<Ref<Error | undefined>>;
  refresh: () => Promise<void>;
  save: (path: string, tags: string[]) => Promise<void>;
}

export const [injectTags, provideTags] = useProvideInject<Tags>("Tags");
