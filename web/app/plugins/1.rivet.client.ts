export default defineNuxtPlugin(() => {
  const { restore } = useConnection();
  restore();
});
