export default defineNuxtPlugin(async () => {
  const { restore } = useConnection();
  restore();

  const { isConnected } = useConnection();
  if (isConnected.value) {
    const { fetchUsers } = useUser();
    await fetchUsers().catch(() => {});
  }
});
