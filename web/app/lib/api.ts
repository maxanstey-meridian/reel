import createClient from "openapi-fetch";
import type { paths, components } from "~~/generated/rivet/schema";

export type FileNode = components["schemas"]["FileNode"];
export type Favourite = components["schemas"]["Favourite"];
export type WatchProgress = components["schemas"]["WatchProgress"];
export type SubtitleTrack = components["schemas"]["SubtitleTrack"];
export type TrickplayMetadata = components["schemas"]["TrickplayMetadata"];
export type PlaybackSession = components["schemas"]["PlaybackSessionResponse"];
export type PlaybackIntent = components["schemas"]["PlaybackIntentResponse"];
export type PlaybackObservation = components["schemas"]["PlaybackObservationResponse"];
export type PlaybackDesiredState = components["schemas"]["PlaybackDesiredStateDto"];
export type PlaybackObservationState = components["schemas"]["PlaybackObservationStateDto"];
export type StartPlaybackRequest = components["schemas"]["StartPlaybackSessionRequest"];
export type UpdatePlaybackIntentRequest = components["schemas"]["UpdatePlaybackIntentRequest"];

type ApiResult<T> = {
  data?: T;
  error?: unknown;
  response: Response;
};

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: unknown,
  ) {
    const message =
      typeof body === "object" && body !== null && "error" in body && typeof body.error === "string"
        ? body.error
        : `API request failed with status ${status}`;
    super(message);
    this.name = "ApiError";
  }
}

let client = createClient<paths>();

export const configureApi = (baseUrl: string, secretKey?: string): void => {
  client = createClient<paths>({
    baseUrl,
    headers: secretKey ? { "X-Secret-Key": secretKey } : undefined,
  });
};

export const validateApiConnection = async (baseUrl: string, key: string) => {
  const candidate = createClient<paths>({ baseUrl });
  return unwrap(await candidate.POST("/api/connect", { body: { key } }));
};

const unwrap = <T>({ data, error, response }: ApiResult<T>): T => {
  if (!response.ok || error !== undefined) {
    throw new ApiError(response.status, error);
  }
  return data as T;
};

export const api = {
  connect: async (key: string) => unwrap(await client.POST("/api/connect", { body: { key } })),
  library: async () => unwrap(await client.GET("/api/library")),
  search: async (q: string, signal?: AbortSignal) =>
    unwrap(await client.GET("/api/search", { params: { query: { q } }, signal })),
  progressAll: async () => unwrap(await client.GET("/api/progress")),
  progressUpdate: async (body: components["schemas"]["ProgressRequest"]) =>
    unwrap(await client.POST("/api/progress", { body })),
  favouritesAll: async () => unwrap(await client.GET("/api/favourites")),
  favouritesAdd: async (body: components["schemas"]["FavouriteRequest"]) =>
    unwrap(await client.POST("/api/favourites", { body })),
  favouritesRemove: async (path: string) =>
    unwrap(await client.DELETE("/api/favourites", { params: { query: { path } } })),
  subtitlesList: async (path: string, signal?: AbortSignal) =>
    unwrap(await client.GET("/api/files/subtitles", { params: { query: { path } }, signal })),
  trickplayInfo: async (path: string, signal?: AbortSignal) =>
    unwrap(await client.GET("/api/files/trickplay", { params: { query: { path } }, signal })),
  // Binary requests use fetch so credentials stay in headers rather than URL/query logs.
  trickplaySprite: async (
    baseUrl: string,
    secretKey: string,
    path: string,
    sheet: number | null,
    signal?: AbortSignal,
  ) => {
    const url = new URL("/api/files/trickplay/sprite", baseUrl);
    url.searchParams.set("path", path);
    if (sheet !== null) {
      url.searchParams.set("sheet", String(sheet));
    }
    const response = await fetch(url, { headers: { "X-Secret-Key": secretKey }, signal });
    if (!response.ok) {
      throw new ApiError(response.status, await response.text());
    }
    return response.blob();
  },
  playbackStart: async (body: StartPlaybackRequest) =>
    unwrap(await client.POST("/api/playback/session", { body })),
  playbackGet: async () => unwrap(await client.GET("/api/playback/session")),
  playbackUpdateIntent: async (body: UpdatePlaybackIntentRequest) =>
    unwrap(await client.PUT("/api/playback/session/intent", { body })),
};
