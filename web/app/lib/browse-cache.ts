// Saved snapshots let pages render immediately while the server copy loads, and stand in when it fails.
const MAX_AGE = 24 * 60 * 60 * 1000;
const PREFIX = "reel:browse:v1:";

export function readBrowseCache<T>(name: string): T | null {
  try {
    const raw = localStorage.getItem(PREFIX + name);
    if (!raw) {
      return null;
    }
    const saved = JSON.parse(raw);
    if (!Number.isFinite(saved.savedAt) || Date.now() - saved.savedAt >= MAX_AGE) {
      localStorage.removeItem(PREFIX + name);
      return null;
    }
    return saved.data ?? null;
  } catch {
    return null;
  }
}

export function writeBrowseCache<T>(name: string, data: T): void {
  try {
    localStorage.setItem(PREFIX + name, JSON.stringify({ savedAt: Date.now(), data }));
  } catch {
    /* Browsing still works when storage is unavailable or full. */
  }
}
