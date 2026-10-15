import { client, pairingKey, unwrap } from "~~/app/lib/api";

export const useConnection = () => {
  const isConnected = computed(() => !!pairingKey.value);

  // The code is kept only once the server accepts it, so a mistyped code leaves a working pairing intact.
  const connect = async (key: string) => {
    unwrap(await client.POST("/api/connect", { body: { key } }));
    pairingKey.value = key;
  };

  const disconnect = () => {
    pairingKey.value = "";
  };

  return { isConnected, connect, disconnect };
};
