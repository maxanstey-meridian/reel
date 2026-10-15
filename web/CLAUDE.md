# Reel web

Nuxt 4 SPA, Vue 3, Nuxt UI 3 / Tailwind 4, TypeScript, pnpm. Production static
output is served by the .NET server; development runs on port 3000.

- Composables call `client` from `app/lib/api.ts` (openapi-fetch, generated types) and
  `unwrap` the result; its middleware owns the pairing key, timeouts and error mapping.
- The app is same-origin with the API; `pnpm dev` proxies `/api` to `REEL_API_URL`
  (default `http://localhost:8080`).
- Layout-wired capabilities are ports (`ports/`, `features/playback/ports/`): the default
  layout calls `provideX`, components call `injectX`.
- Tests live in `tests/`, mirroring `app/`; they stand in for the server with
  `tests/support/fakeServer.ts` rather than mocking modules.
- `generated/rivet/openapi.json` and `schema.d.ts` are generated, never hand edited.
- Regenerate contracts with `task web:rivet` after changing server routes.
- `features/playback/usePlaybackSession.ts` owns layout-shared session polling.
- Pages compose UI; pure playback/scrub logic lives in `features/playback/logic`.
- Use normal Vue state and browser controls. No new state framework or command bus.
- Player truth comes from TV observations; pending requests are shown as pending.
- Phone is a remote, not a second media player.

Use script setup with TypeScript, typed props/emits, Nuxt UI for standard controls,
and the existing dark mobile layout. Test user behaviour, not incidental structure.
`pnpm check`, `pnpm lint`, `pnpm test`, `pnpm build` and `pnpm e2e` are available.
`pnpm generate` builds the static phone UI for the combined production server.
