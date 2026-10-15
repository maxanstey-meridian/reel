// Saved snapshots let pages render immediately while the server copy loads, and stand in when it fails.
const MAX_AGE = 24 * 60 * 60 * 1000;
const ROOT = "reel:browse:";
// Keyed by build, so a deploy never reads a snapshot shaped by older code.
const prefix = (build: string): string => `${ROOT}${build}:`;

export function readBrowseCache<T>(build: string, name: string): T | null {
  try {
    const raw = localStorage.getItem(prefix(build) + name);
    if (!raw) {
      return null;
    }
    const saved: unknown = JSON.parse(raw);
    if (
      typeof saved !== "object" ||
      saved === null ||
      !("savedAt" in saved) ||
      typeof saved.savedAt !== "number" ||
      Date.now() - saved.savedAt >= MAX_AGE
    ) {
      localStorage.removeItem(prefix(build) + name);
      return null;
    }
    // This build wrote it from a T.
    return "data" in saved ? ((saved.data as T) ?? null) : null;
  } catch {
    return null;
  }
}

export function writeBrowseCache<T>(build: string, name: string, data: T): void {
  try {
    const current = prefix(build);
    // Earlier builds' snapshots are never read again; drop them so they don't fill storage.
    for (const key of Object.keys(localStorage)) {
      if (key.startsWith(ROOT) && !key.startsWith(current)) {
        localStorage.removeItem(key);
      }
    }
    localStorage.setItem(current + name, JSON.stringify({ savedAt: Date.now(), data }));
  } catch {
    /* Browsing still works when storage is unavailable or full. */
  }
}
