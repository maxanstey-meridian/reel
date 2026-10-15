import type { ComputedRef, Ref } from "vue";
import { useProvideInject } from "~~/app/composables/useProvideInject";
import type { components } from "~~/generated/rivet/schema";

export type Favourite = components["schemas"]["Favourite"];

export interface Favourites {
  favourites: Readonly<Ref<Favourite[]>>;
  loading: ComputedRef<boolean>;
  error: Readonly<Ref<Error | undefined>>;
  refresh: () => Promise<void>;
  isFavourite: (relativePath: string) => boolean;
  toggle: (relativePath: string) => Promise<void>;
}

export const [injectFavourites, provideFavourites] = useProvideInject<Favourites>("Favourites");
