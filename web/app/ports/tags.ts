import { useProvideInject } from "~~/app/composables/useProvideInject";

export interface Tags {
  forPath: (path: string) => string[];
  /** A save replaces an item's whole set, so edits wait until the saved sets have loaded. */
  loaded: Readonly<Ref<boolean>>;
  save: (path: string, tags: string[]) => Promise<void>;
}

export const [injectTags, provideTags] = useProvideInject<Tags>("Tags");
