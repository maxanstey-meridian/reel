/**
 * API client for the Reel server.
 *
 * All server communication goes through this module.
 * The secret key is attached to every request except POST /api/connect.
 */

type FetchOptions = RequestInit & { params?: Record<string, string> };

export const createApiClient = (baseUrl: string, secretKey: string) => {
  const apiFetch = async <T>(path: string, options: FetchOptions = {}): Promise<T> => {
    const url = new URL(path, baseUrl);

    if (options.params) {
      for (const [k, v] of Object.entries(options.params)) {
        url.searchParams.set(k, v);
      }
    }

    const headers = new Headers(options.headers);
    if (!headers.has("X-Secret-Key")) {
      headers.set("X-Secret-Key", secretKey);
    }

    const response = await fetch(url.toString(), { ...options, headers });

    if (!response.ok) {
      throw new ApiError(response.status, await response.text());
    }

    return response.json() as Promise<T>;
  };

  return { apiFetch };
};

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: string,
  ) {
    super(`API ${status}: ${body}`);
    this.name = "ApiError";
  }
}
