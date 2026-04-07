# CLAUDE.md — Reel Web UI

Mobile-first SPA for browsing a media library and casting to a Fire TV. Nuxt 3, @nuxt/ui, SPA mode. The server is a separate .NET process at `../server/`.

## Stack

- Nuxt 3 (SPA — `ssr: false`)
- @nuxt/ui 3 (components + Tailwind v4)
- Rivet (generated typed API client from server contracts)
- Vitest (unit tests)
- Playwright (e2e tests)
- pnpm

## Key files

| File | Purpose |
|---|---|
| `generated/rivet/client/reel.ts` | Generated typed API client — **do not edit** |
| `generated/rivet/types/models.ts` | Generated TS types (FileNode, ConnectResponse, etc.) — **do not edit** |
| `app/plugins/1.rivet.client.ts` | Bootstraps Rivet with server URL from runtimeConfig |
| `app/lib/api.ts` | Raw fetch wrapper — only for non-Rivet calls (e.g. file stream URLs) |
| `nuxt.config.ts` | SPA mode, @nuxt/ui module, `serverUrl` in runtimeConfig.public |
| `e2e/` | Playwright tests + helpers |

## API client

**Always use the Rivet generated client for server API calls.** Never use raw `fetch` or `$fetch` for endpoints that have a contract.

```typescript
import { reel } from "~~/generated/rivet/client";
import type { FileNode } from "~~/generated/rivet/types/models";

const tree = await reel.library();           // → FileNode
const result = await reel.connect({ key }); // → ConnectResponse
const health = await reel.health();          // → HealthResponse
```

The stream endpoint (`/api/files/stream?path=...`) is **not** in the Rivet contract — it returns raw bytes, not JSON. Construct stream URLs manually:

```typescript
const streamUrl = `${serverUrl}/api/files/stream?path=${encodeURIComponent(relativePath)}`;
```

### Regenerating the client

When a server endpoint is added or changed:

1. Update `server/Reel.Server/ReelContract.cs`
2. Run `task web:rivet` (or `pnpm rivet`)
3. New typed functions appear in `generated/rivet/client/reel.ts` automatically

## Architecture

### The dependency rule

```
logic/         ← pure TS, zero Vue/Nuxt imports (directly unit-testable)
composables/   ← thin reactive shells, hold refs, call Rivet client
pages/         ← composition roots
components/    ← presentational only, no side effects
```

Each layer may only import from layers above it. `logic/` depends on nothing. `components/` may import from `logic/` directly since logic has zero dependencies.

### When to use ports/adapters

Most composables in this app will be simple: reactive state + a Rivet call. **Don't force the ports/adapters pattern on simple composables.** The pattern earns its keep when a composable mixes infrastructure concerns with non-trivial state logic.

If a composable grows complex enough to warrant it:

```
pages/<feature>/
├── ports/           ← type contracts + typed injection keys
├── adapters/        ← pure implementations satisfying port types
├── logic/           ← pure TS functions (unit-testable)
│   └── __tests__/   ← Vitest tests
├── composables/     ← reactive shells, inject ports
└── components/      ← feature-specific presentational components
```

Rules:
- Port files contain only the type and the injection key. No logic.
- Adapters have zero Vue/Nuxt imports. If it needs `ref`, it's not an adapter.
- Composables never import from `adapters/` — they inject the port.
- Feature-local ports live in `pages/<feature>/ports/`. Promote to `app/ports/` when a second feature needs it.

### Colocation

Features own their files. Shared utilities live at `app/` level only when consumed by 2+ features.

### What stays outside this pattern

- **Rivet clients** — already generated typed contracts, use directly in composables.
- **Simple composables** (reactive state + API call) — don't force the full pattern.
- **One-off utils** — a 5-line helper doesn't need a port.

## Styling

- **Tailwind v4** via @nuxt/ui — utility classes available globally.
- **@nuxt/ui components** (`UCard`, `UButton`, `UInput`, `UBadge`, etc.) for all standard UI chrome.
- **Dark theme by default** — this is a media app, dark background is expected.
- **Custom CSS** in `app/assets/css/app.css` — component classes like `.page-title`, `.content-canvas`. Use these for repeated layout patterns instead of long Tailwind chains.
- **No arbitrary values** where a semantic utility exists. Prefer `gap-4` over `gap-[1rem]`.

## Testing

### Unit tests (Vitest)

Test pure logic functions. No component mounting for logic tests.

```bash
task web:test     # or pnpm test
```

Config: `vitest.config.ts`. Tests live in `logic/__tests__/` colocated with the logic they test.

### E2e tests (Playwright)

Test user flows against a running Nuxt + server.

```bash
task web:e2e      # or pnpm e2e
```

Config: `playwright.config.ts`. Tests in `e2e/`. Playwright auto-starts Nuxt dev server.

Patterns:
- `spyResponse(page, urlPattern)` — wait for an API call to complete before asserting UI state (from `e2e/utils/network.ts`)
- `.env.test` has test credentials, loaded via Zod schema in `e2e/env.ts`

## Task runner

```bash
task web:rivet    # Regenerate Rivet client from server contract
task web:dev      # Nuxt dev server on :3000
task web:build    # Production build
task web:test     # Vitest
task web:e2e      # Playwright
task server:run   # .NET server on :8080 (needed for dev)
```

## Dev workflow

Two terminals:

```bash
task server:run   # terminal 1 — API on :8080
task web:dev      # terminal 2 — SPA on :3000
```

The web app talks to the server via `runtimeConfig.public.serverUrl` (defaults to `http://localhost:8080`).

## Conventions

- `const` arrow functions, not `function` declarations.
- `<script setup lang="ts">` in all Vue SFCs.
- Typed props and emits — no `any`.
- File naming: kebab-case for files, PascalCase for components in templates.
- Composables: `use` prefix, camelCase (`useLibrary`, `useCast`).
- No `process.env` — use `useRuntimeConfig()`.
