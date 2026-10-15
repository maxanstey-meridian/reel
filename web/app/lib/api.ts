import { useLocalStorage } from "@vueuse/core";
import createClient from "openapi-fetch";
import type { components, paths } from "~~/generated/rivet/schema";

export type FileNode = components["schemas"]["FileNode"];
export type PlaybackSession = components["schemas"]["PlaybackSessionResponse"];
export type PlaybackDesiredState = components["schemas"]["PlaybackDesiredStateDto"];
export type TrickplayMetadata = components["schemas"]["TrickplayMetadata"];
type ErrorBody = components["schemas"]["ErrorResponse"];

/** No answer: the Studio is off, asleep, or not on this network. */
export class ServerUnreachableError extends Error {
  constructor(cause: unknown) {
    super("Can't reach the Reel server", { cause });
  }
}

// The phone app is served by the Reel server itself, so every request is same-origin.
export const pairingKey = useLocalStorage("reel:secretKey", "");
// fetch is looked up per request rather than captured once, so tests can stand in for the server.
export const client = createClient<paths>({ fetch: (request) => globalThis.fetch(request) });

const REQUEST_TIMEOUT_MS = 15_000;
// A rescan walks the whole media folder, which can take minutes on a large library.
const RESCAN_TIMEOUT_MS = 10 * 60_000;

client.use({
  onRequest: ({ request, schemaPath }) => {
    const timeout = AbortSignal.timeout(
      schemaPath === "/api/library/rescan" ? RESCAN_TIMEOUT_MS : REQUEST_TIMEOUT_MS,
    );
    const timed = new Request(request, { signal: AbortSignal.any([request.signal, timeout]) });
    if (pairingKey.value) {
      timed.headers.set("X-Secret-Key", pairingKey.value);
    }
    return timed;
  },
  onResponse: ({ response, schemaPath }) => {
    // A rejected pairing code is answered on the connect page itself.
    if (response.status === 401 && schemaPath !== "/api/connect") {
      pairingKey.value = "";
      void navigateTo("/connect", { replace: true });
    }
  },
  // Callers cancel superseded requests and expect the abort itself.
  onError: ({ error }) =>
    error instanceof DOMException && error.name === "AbortError"
      ? undefined
      : new ServerUnreachableError(error),
});

// Every openapi-fetch result; constraining `error` to ErrorResponse makes an endpoint that
// declares any other error shape a compile error here.
type ApiResult = { data?: unknown; error?: ErrorBody; response: Response };
type Success<R extends ApiResult> = Extract<R, { data: unknown }>["data"];

export const unwrap = <R extends ApiResult>(result: R): Success<R> => {
  if (result.response.ok) {
    return result.data as Success<R>;
  }
  // The server answered, but refused or failed the request.
  throw new Error(
    result.error?.message ??
      `The Reel server could not complete the request (${result.response.status})`,
  );
};

// <img> and CSS backgrounds cannot send headers, so file URLs carry the key as `token`.
const fileUrl = (route: string, query: Record<string, string>): string =>
  `${route}?${new URLSearchParams({ ...query, token: pairingKey.value })}`;

export const thumbnailUrl = (path: string): string => fileUrl("/api/files/thumbnail", { path });

export const trickplaySpriteUrl = (path: string, sheet: number): string =>
  fileUrl("/api/files/trickplay/sprite", { path, sheet: String(sheet) });
