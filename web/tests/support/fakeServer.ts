import { vi } from "vitest";

type Handler = (request: Request) => Response | Promise<Response>;

const routeOf = (request: Request): string => `${request.method} ${new URL(request.url).pathname}`;

/**
 * Stands in for the Reel server behind `fetch`, keyed by "METHOD /path". Tests may replace a
 * route's handler at any point; unrouted requests get a 404 in the server's error shape.
 */
export const fakeServer = (routes: Record<string, Handler> = {}) => {
  const received: Request[] = [];
  vi.stubGlobal("fetch", async (request: Request) => {
    received.push(request);
    const handler = routes[routeOf(request)];
    return handler
      ? handler(request)
      : Response.json({ code: "not_found", message: "Not found" }, { status: 404 });
  });
  return {
    routes,
    requests: (route: string) => received.filter((request) => routeOf(request) === route),
  };
};

/** Answers like a real server that never replies: only an abort ends the request. */
export const neverAnswers = (request: Request): Promise<Response> =>
  new Promise((_resolve, reject) => {
    if (request.signal.aborted) {
      reject(request.signal.reason);
    }
    request.signal.addEventListener("abort", () => reject(request.signal.reason));
  });

export const deferred = <T>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
};
