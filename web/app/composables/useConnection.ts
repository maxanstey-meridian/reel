import { configureApi, validateApiConnection } from "~~/app/lib/api";

const STORAGE_KEY_URL = "reel:serverUrl";
const STORAGE_KEY_SECRET = "reel:secretKey";

export const useConnection = () => {
  const serverUrl = useState<string>("reel:serverUrl", () => "");
  const secretKey = useState<string>("reel:secretKey", () => "");

  const isConnected = computed(() => !!serverUrl.value && !!secretKey.value);

  const connect = async (url: string, key: string) => {
    await validateApiConnection(url, key);
    configureApi(url, key);

    localStorage.setItem(STORAGE_KEY_URL, url);
    localStorage.setItem(STORAGE_KEY_SECRET, key);
    serverUrl.value = url;
    secretKey.value = key;
  };

  const disconnect = () => {
    localStorage.removeItem(STORAGE_KEY_URL);
    localStorage.removeItem(STORAGE_KEY_SECRET);
    serverUrl.value = "";
    secretKey.value = "";
    configureApi("");
  };

  const restore = () => {
    const storedUrl = localStorage.getItem(STORAGE_KEY_URL);
    const storedKey = localStorage.getItem(STORAGE_KEY_SECRET);

    if (storedUrl && storedKey) {
      configureApi(storedUrl, storedKey);
      serverUrl.value = storedUrl;
      secretKey.value = storedKey;
    }
  };

  return { serverUrl, secretKey, isConnected, connect, disconnect, restore };
};
