# Reel MVP Rebuild Plan

## 1. Purpose

Reel is a personal, self-hosted media player for one trusted local network.

The product has three parts:

- a small .NET server running beside the media files;
- a mobile-first web app used as the library and remote control;
- a thin Fire TV app used as the playback target.

The defining interaction is:

```text
Select on phone -> play on TV -> control from phone -> observe actual TV state
```

Reel deliberately does not try to be Plex or Jellyfin. It owns the pairing and
direct-streaming semantics because those are the parts that must remain simple,
predictable, and reliable on the local network.

This document is the authoritative product and delivery plan after the August
2026 teardown. `GAP_PLAN.md` describes older work and is not the target design.

## 2. Product Principles

### 2.1 Phone owns intent

The mobile app owns:

- browsing;
- search;
- media selection;
- starting and stopping playback;
- play and pause;
- seeking and trickplay previews;
- subtitle selection;
- favourites;
- resume or start-over decisions.

### 2.2 TV owns playback reality

The TV app owns:

- ExoPlayer and Media3 lifecycle;
- buffering and playback errors;
- the actual position and duration;
- applying phone intent;
- progress reporting;
- polished transient playback feedback.

The TV has no library browser. Hardware Play/Pause remains as a fallback. Media
selection and seeking remain phone-driven.

### 2.3 Server coordinates and persists

The server owns:

- pairing and trusted-LAN access;
- the indexed media catalogue;
- direct range streaming;
- search;
- favourites;
- durable progress;
- subtitle and trickplay generation;
- one non-persistent live playback session.

### 2.4 Observed state beats optimistic state

The mobile app must not claim that playback changed because a request was
accepted. It displays the TV's observed state. A pending operation may be shown
as pending, but success comes from the TV observation.

### 2.5 KISS constraints

- One server.
- One active TV playback session.
- One trusted local network.
- Direct play only for the MVP.
- HTTP polling, not WebSockets or SignalR.
- SQLite for durable state.
- In-memory live session state.
- No message broker, command bus, event sourcing, or generic repository layer.
- Generated OpenAPI types are the cross-stack contract boundary.

## 3. Settled Scope

### 3.1 MVP capabilities

- TV discovers and pairs with the Reel server.
- Phone pairs through QR parameters or manual server URL and key entry.
- Phone browses the media library.
- Phone searches by parsed title and relative path.
- Phone adds and removes favourites.
- Phone starts a playable file on the TV.
- Phone chooses Resume or Start Over when durable progress exists.
- Phone plays, pauses, stops, and seeks.
- Phone shows trickplay frames while dragging the seek control.
- TV gives polished loading, buffering, play, pause, seek, and error feedback.
- TV reports actual position, duration, state, and selected subtitle.
- Progress survives app and server restarts.
- Watched state is derived from progress at or above 90 percent.
- Phone selects an available subtitle track or disables subtitles.
- Pairing and playback disconnection are recoverable without deleting media data.

### 3.2 Explicit non-goals

Do not rebuild these without a new product decision:

- profiles or users;
- collections;
- tags;
- metadata editing;
- TMDB integration;
- cast queues or Up Next;
- auto-advance;
- Recently Added;
- poster mosaics or TV library statistics;
- TV library navigation;
- advanced manual watched overrides;
- alphabet scrolling;
- PWA MediaSession integration;
- server-generated thumbnails separate from trickplay;
- transcoding;
- remote internet access;
- multiple TV selection;
- background mobile playback control.

## 4. Current Baseline

The teardown intentionally left a compilable foundation rather than a usable
end-to-end product.

### 4.1 Server retained surface

The server currently retains:

- `/api/connect`;
- `/api/health`;
- `/pair`;
- `/api/library`;
- `/api/search`;
- `/api/files/stream`;
- `/api/progress`;
- `/api/favourites`;
- `/api/files/subtitles`;
- `/api/files/subtitles/content`;
- `/api/files/trickplay`;
- `/api/files/trickplay/sprite`.

The disposable SQLite database is `reel-v2.db` and contains only media,
progress, favourites, subtitles, and trickplay data.

### 4.2 Mobile retained surface

The web app currently has:

- connection restoration and manual/QR connection;
- Library, Search, and Favourites navigation;
- progress and Continue Watching data;
- media details;
- one typed `openapi-fetch` boundary in `web/app/lib/api.ts`.

It intentionally has no Play action or Now Playing UI.

### 4.3 TV retained surface

The TV app currently has:

- mDNS server discovery;
- pairing persistence;
- Pairing and Ready screens;
- a retained but unreachable ExoPlayer `PlayerScreen`;
- progress reporting;
- subtitle loading;
- trickplay and local scrub UI;
- playback error classification.

### 4.4 Baseline verification status

The reduced baseline and rebuild are verified locally against Rivet 0.41,
OpenAPI-generated TypeScript, Android API 35, and the real server-backed browser
journey. Device-specific Fire TV behavior still requires manual verification.

## 5. Target Architecture

### 5.1 End-to-end flow

```text
Mobile web                     Reel server                      Fire TV
    |                               |                              |
    | POST playback session        |                              |
    |------------------------------>|                              |
    |                               |<---- GET playback session ----|
    |                               |                              |
    |                               |       apply intent           |
    |                               |                              |
    |                               |<--- PUT observation ----------|
    |<---- GET playback session ----|                              |
    |                               |                              |
    | PUT intent (pause/seek/etc.)  |                              |
    |------------------------------>|                              |
    |                               |<---- GET playback session ----|
    |                               |                              |
    |                               |<--- POST progress ------------|
```

### 5.2 Server structure

Keep the existing flat layers. Add only:

```text
server/Reel.Server/
  Application/
    PlaybackSessionService.cs
    Models/
      PlaybackRequests.cs
  Domain/
    PlaybackSession.cs
  Endpoints/
    PlaybackSessionEndpoints.cs
```

`PlaybackSessionService` is an in-memory application service. It requires no
persistence port. It must be thread-safe and use `TimeProvider` for deterministic
expiry tests.

The server continues to expose its API through Rivet attributes, generated
OpenAPI, and generated TypeScript types. Generated web files are never edited
manually.

### 5.3 Mobile structure

Keep the current simple composable/page structure. Add:

```text
web/app/
  components/
    MiniPlayer.vue
    PlaybackScrubber.vue
    TrickplayPreview.vue
  composables/
    usePlaybackSession.ts
  pages/
    now-playing.vue
```

The default layout owns one playback-session instance and exposes it to its
pages/components. Do not create parallel playback state in media cards, detail
pages, and Now Playing.

The persistent navigation remains Library, Search, and Favourites. An active
session appears as a mini-player above navigation; selecting it opens Now
Playing.

### 5.4 TV structure

Refactor player ownership out of the composable:

```text
tv/app/src/main/java/com/max/reel/
  data/
    ReelApi.kt
    ApiClient.kt
  playback/
    PlaybackViewModel.kt
    PlaybackState.kt
  ui/player/
    PlayerScreen.kt
```

`PlaybackViewModel` owns:

- one ExoPlayer instance;
- one Media3 `MediaSession`;
- server session polling;
- intent reconciliation;
- observed-state reporting;
- durable progress reporting;
- player release.

`PlayerScreen` renders the player and state. It does not create the player,
poll the server, or write progress directly.

Use `MediaSession`, not `MediaSessionService`, for the MVP. Reel does not promise
background TV playback, and the activity remains visible while playing. Move to
a service only if background playback becomes an explicit requirement.

Add the Media3 session dependency. Retain Ktor, ExoPlayer, Coil, ZXing, and the
existing Android NSD discovery mechanism.

## 6. Playback Session Contract

### 6.1 Domain model

The server holds at most one session:

```text
PlaybackSession
  sessionId: UUID
  revision: long
  createdAtUtc: instant
  updatedAtUtc: instant
  intent: PlaybackIntent
  observation: PlaybackObservation?

PlaybackIntent
  relativePath: string
  desiredState: playing | paused | stopped
  subtitleIndex: int?
  pendingSeek: SeekRequest?

SeekRequest
  seekId: long
  positionSeconds: double

PlaybackObservation
  appliedRevision: long
  appliedSeekId: long?
  state: idle | loading | buffering | playing | paused | ended | error
  relativePath: string?
  positionSeconds: double
  durationSeconds: double
  subtitleIndex: int?
  errorCode: string?
  errorMessage: string?
  updatedAtUtc: instant
```

The public response also includes `tvConnected`, derived from observation
freshness. It is not persisted as independent state.

### 6.2 Endpoints

#### `POST /api/playback/session`

Start or replace the current session.

Request:

```text
relativePath
startPositionSeconds
subtitleIndex?
```

Rules:

- canonicalise and validate the media path before creating the session;
- reject missing or unsupported files;
- set desired state to `playing`;
- create a pending seek when start position is greater than zero;
- replace any existing session intentionally;
- return the complete new session.

#### `GET /api/playback/session`

Return an envelope containing the current session or `null`. Always returning a
typed envelope avoids an ambiguous 204 response in generated clients.

#### `PUT /api/playback/session/intent`

Request:

```text
sessionId
desiredState
subtitleIndex?
seekPositionSeconds?
```

Rules:

- reject a stale or unknown `sessionId`;
- replace stable desired state and subtitle selection;
- increment `revision` on every accepted update;
- when a seek is supplied, allocate a new monotonic `seekId`;
- when no seek is supplied, retain any unacknowledged pending seek;
- validate seek position as finite, non-negative, and not beyond known duration;
- return the complete updated session.

There is no optimistic success in the mobile UI. Acceptance only means that the
server recorded intent.

#### `PUT /api/playback/session/observation`

The TV reports actual player state.

Rules:

- reject an unknown `sessionId`;
- ignore an observation older than the stored applied revision;
- clear pending seek after `appliedSeekId` acknowledges it;
- validate finite, non-negative position and duration;
- update observation freshness;
- never write durable progress from this endpoint.

### 6.3 Reconciliation rules

- TV polls once per second while paired.
- TV applies stable intent only when `revision` advances.
- TV applies a seek only when `seekId` is newer than its last applied seek.
- Multiple phone seeks before a TV poll collapse to the latest position.
- TV reports immediately on state transition and once per second while active.
- Phone polls once per second while a session exists.
- `tvConnected` becomes false after five seconds without a TV observation.
- A disconnected session remains available for reconnection; the UI says that
  the TV is disconnected rather than pretending playback continues.
- A stopped or ended session remains visible for 30 seconds, then expires.
- Any non-terminal session expires after 30 minutes without an observation.
- A server restart loses live session state but not durable progress.

## 7. Durable Progress Rules

Progress remains independent from live playback state.

The TV writes progress:

- after the media duration becomes known;
- every 10 seconds while playing;
- after a committed seek;
- when paused;
- when stopped;
- when playback ends;
- immediately before player release when possible.

Rules:

- duration comes from ExoPlayer, never filename or browser estimates;
- position and duration must be finite and non-negative;
- position is clamped to duration;
- progress at or above 90 percent derives `watched = true`;
- Resume uses the persisted position;
- Start Over begins at zero and writes zero once playback begins;
- terminal completion records duration as position;
- failed progress writes do not alter live observed state.

## 8. Trickplay and Seeking

The phone is the primary seek surface.

Flow:

1. Now Playing requests trickplay metadata for the current media.
2. Dragging computes the target frame and sprite coordinates locally.
3. The phone renders the preview without issuing network seeks while dragging.
4. Releasing sends one intent update with `seekPositionSeconds`.
5. The TV applies the new `seekId` once.
6. The TV reports the resulting observed position.
7. The phone settles its scrubber on the observed position.

The TV shows a brief seek overlay and destination time. It does not expose
D-pad trickplay seeking in the MVP. Hardware Play/Pause remains available.

Trickplay generation remains lazy and cached. Generation must share one
cancellable FFmpeg execution boundary and must not spawn duplicate work for the
same media file.

## 9. Pairing and Recovery

Pairing remains Reel-owned.

### 9.1 Security model

The MVP security model is explicitly:

```text
Trusted LAN + shared Reel access key
```

The key is not described as confidential from other LAN participants because it
is advertised for local discovery. Remote internet access is unsupported.

### 9.2 Required pairing behavior

- TV keeps discovering until pairing succeeds or the user cancels.
- A failed discovered server does not permanently end discovery.
- TV can forget its server and return to Pairing.
- Ready screen shows server identity and connection health.
- QR parameters always contain a reachable mobile URL, server URL, and key.
- Mobile validates `/api/connect` before persisting credentials.
- Mobile disconnect clears credentials and returns to Connect.
- Pairing errors offer Retry and manual entry where relevant.

## 10. Library and Search Foundation

Search must not depend on the user visiting Library first.

### 10.1 Required scanner behavior

- scan the media root at server startup before serving catalogue results;
- expose an authenticated explicit rescan operation;
- upsert added and changed playable files;
- remove database rows for deleted files;
- cascade removal to progress, favourites, subtitles, and trickplay metadata;
- parse title and year deterministically;
- do not generate subtitles or trickplay during the catalogue scan;
- do not mutate the index as a side effect of read endpoints.

### 10.2 Search behavior

- case-insensitive title and relative-path matching;
- stable deterministic ordering;
- empty query returns an empty result, not the whole library;
- only playable files appear;
- no tags, genres, watched filters, or metadata-provider search.

SQLite FTS5 may be introduced only if measured library size makes the simple
query inadequate. It is not required pre-emptively.

## 11. Media Path and Streaming Invariants

All file-backed endpoints use one canonical media-path resolver.

It must:

- reject rooted client paths;
- resolve against the configured media root;
- reject traversal outside the root;
- reject symlink escape outside the root;
- reject the `.reel` internal directory;
- reject non-playable files for stream/session start;
- allow only known subtitle/cache files from their dedicated endpoints;
- preserve HTTP range processing for video streams.

No endpoint accepts an arbitrary absolute path. The database file must never be
streamable.

## 12. Delivery Plan

Each phase must leave its own tests green. Do not start the next phase with a
known-red baseline.

### Phase 0: Stabilise the reduced baseline

Status: complete.

- finish the Rivet 0.41 attributed-contract migration;
- generate `openapi.json` and `schema.d.ts` through `task web:rivet`;
- remove all imports of deleted generated clients;
- make server restore/build/test pass with NuGet auditing enabled;
- make web typecheck, lint, unit tests, and production build pass;
- configure an Android SDK and make TV unit tests/build pass;
- delete the previous Reel database before manual execution;
- remove stale feature references from documentation and E2E tests;
- add root `check` and `ci` Taskfile tasks after the individual commands are
  reliable.

Exit criteria:

- all three applications compile;
- retained tests are green;
- generated files reproduce with no unexpected diff;
- no deleted endpoint or feature name remains in production code.

### Phase 1: Make catalogue, path handling, and pairing reliable

Status: complete.

- centralise media-path resolution;
- add startup index reconciliation;
- add explicit rescan;
- make search independent from Library reads;
- fix TV discovery retry and forget-pairing flow;
- guarantee a valid mobile pairing URL;
- add pairing and path-security integration tests.

Exit criteria:

- a fresh server can answer Search before Library is opened;
- deleted files disappear after rescan;
- `.reel`, traversal, and symlink escapes are rejected;
- phone and TV can both recover from failed pairing.

### Phase 2: Implement the server playback session

Status: complete.

- add domain records and enums;
- add the thread-safe in-memory service;
- add four session endpoints and Rivet contracts;
- implement revision, seek ID, freshness, and expiry rules;
- regenerate OpenAPI and TypeScript types;
- add comprehensive service and endpoint tests.

Exit criteria:

- tests prove start, replace, pause, play, seek collapse, subtitle change,
  observation, stale-TV detection, terminal expiry, and server-restart loss;
- no playback state is persisted to SQLite;
- invalid and stale session operations return explicit errors.

### Phase 3: Connect TV to the session

Status: implementation complete; real Fire TV verification remains.

- introduce the Ktor `ReelApi` boundary for fakeable protocol tests;
- add Media3 session dependency;
- move ExoPlayer ownership into `PlaybackViewModel`;
- poll and reconcile server intent;
- report observed state and durable progress;
- simplify `PlayerScreen` to rendering and local Play/Pause fallback;
- route MainActivity between Pairing, Ready, and Player from observed app state;
- remove TV D-pad seek behavior;
- add retry/backoff and disconnected UI.

Exit criteria:

- posting a session through an API test starts a real file on Fire TV;
- pause, play, stop, and one seek apply once;
- leaving/re-entering Compose does not create another player;
- progress survives player disposal;
- TV reports actionable playback errors.

### Phase 4: Build the mobile remote

Status: complete.

- add typed playback methods to `web/app/lib/api.ts`;
- add one layout-owned playback session composable;
- add Play to media detail;
- add Resume/Start Over decision;
- add active mini-player;
- add Now Playing from observed state;
- add Play/Pause and Stop;
- add subtitle selection;
- add phone trickplay scrubbing;
- display pending, disconnected, buffering, ended, and error outcomes honestly.

Exit criteria:

- no browser-local value claims a file is playing independently of server
  observation;
- controls remain pending until reflected by TV observation;
- trickplay drag sends no seek until release;
- reload restores the live session from the server;
- another phone sees the same observed session.

### Phase 5: End-to-end hardening

Status: automated hardening complete; real Fire TV acceptance journeys remain.

- exercise the complete journey on the actual Mac and Fire TV;
- add web E2E coverage against a real Reel server and temporary media root;
- add server protocol integration tests;
- add TV coordinator tests with fake API and fake player boundary where useful;
- verify long files, spaces, Unicode filenames, missing files, server restart,
  Wi-Fi interruption, subtitle failure, and trickplay generation failure;
- profile polling and progress write frequency;
- remove diagnostics and temporary compatibility code.

Exit criteria:

- all journeys in Section 14 pass;
- all builds and checks pass from root Taskfile tasks;
- no known control can report success while TV observation disagrees;
- no non-goal has leaked back into the implementation.

## 13. Testing Strategy

### 13.1 Server

Use xUnit against real temporary SQLite databases and temporary filesystem
roots. Fake only clocks and external process execution where deterministic
control is required.

Required test groups:

- playback session state transitions;
- revision and seek idempotency;
- freshness and expiry with fake time;
- canonical path security;
- scan reconciliation;
- progress threshold and validation;
- endpoint contract and error behavior;
- byte-range streaming regression.

### 13.2 Web

Use Vitest for pure trickplay math and composable state transitions. Use
Playwright for user outcomes against the real server.

Do not recreate deleted feature tests. Prefer tests for:

- Resume versus Start Over;
- observed-state rendering;
- pending control behavior;
- stale TV and error recovery;
- one seek on scrub release;
- session restoration after reload.

### 13.3 TV

Keep protocol and reconciliation logic outside Compose so it can be tested with
fake API/player boundaries. Compose tests cover focus and visible outcomes only.

Manual Fire TV verification remains required for:

- Media3 rendering;
- hardware Play/Pause;
- buffering transitions;
- audio/subtitle behavior;
- long-running progress;
- focus and overscan.

## 14. MVP Acceptance Journeys

### Journey A: First-time setup

1. Start Reel server with a fresh media root database.
2. Launch unpaired TV app.
3. TV discovers server and displays pairing details.
4. Open the QR URL on phone.
5. Mobile validates and stores the connection.
6. TV and phone both show a ready, connected state.

### Journey B: Search and play

1. Search for a title without first opening Library.
2. Open the result.
3. Select Play.
4. TV leaves Ready, loads the file, and starts playback.
5. Phone shows loading, then TV-observed playing state.

### Journey C: Remote control

1. Pause from phone.
2. Phone shows pending until TV reports paused.
3. Resume from phone.
4. Drag the phone scrubber and see trickplay frames.
5. Release at a new position.
6. TV seeks once and reports the resulting position.
7. Stop from phone and observe TV return to Ready.

### Journey D: Resume

1. Play past the first progress interval.
2. Stop playback and restart TV or server.
3. Select the same file on phone.
4. Choose Resume and start at the persisted position.
5. Repeat and choose Start Over to begin at zero.

### Journey E: Completion

1. Play or seek beyond 90 percent.
2. Finish playback.
3. TV reports ended and final duration.
4. Library derives the file as watched.
5. The terminal session expires instead of remaining Now Playing forever.

### Journey F: Recovery

1. Interrupt server or Wi-Fi during playback.
2. TV and phone show disconnected without claiming playback commands succeeded.
3. Restore connectivity.
4. TV resynchronises the current intent or exposes a clear retry action.
5. Forget pairing on TV and pair again without clearing progress/favourites.

## 15. Definition of Done

The MVP is done when:

- the six acceptance journeys pass on the actual devices;
- server restore, build, tests, and NuGet audit pass;
- web generated types, typecheck, lint, tests, and build pass;
- TV tests and debug assembly pass;
- direct streaming supports byte ranges and rejects internal/out-of-root files;
- search works from a fresh server process;
- all mobile controls are confirmed by observed TV state;
- position and duration survive restarts;
- there is no production code for any explicit non-goal;
- root documentation and Taskfile commands match actual behavior.

## 16. Change Discipline

- Deliver vertical behavior, not disconnected infrastructure layers.
- Add only abstractions with a second implementation or a meaningful test seam.
- Keep generated Rivet/OpenAPI files generated.
- Do not preserve compatibility with the pre-teardown database.
- Do not add fallback behavior that hides a broken invariant.
- Any new feature must state its owner: phone intent, TV reality, or server
  coordination/persistence.
- If two components can both claim playback truth, the design is wrong.
