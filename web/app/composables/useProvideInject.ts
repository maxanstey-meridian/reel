import { inject, provide, type InjectionKey } from "vue";

export const useProvideInject = <T>(name: string) => {
  const key: InjectionKey<T> = Symbol(name);

  const provideValue = <TImplementation extends T>(implementation: TImplementation): TImplementation => {
    provide(key, implementation);
    return implementation;
  };
  const injectValue = (): T => {
    const implementation = inject(key);
    if (implementation === undefined) {
      throw new Error(`${name} is not provided`);
    }
    return implementation;
  };

  return [injectValue, provideValue] as const;
};
