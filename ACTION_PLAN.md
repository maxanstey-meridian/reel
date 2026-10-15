# Reel: finish the personal player

Implemented 2026-09-08. Source and built APK are in this workspace.

- Complete: simpler progress saving/observation reporting, live phone position,
  consistent 95% resume/completion rules, phone skip/remaining time/scrub cancel,
  TV seeking/screen-awake handling, library rescan, connection status, next episode,
  combined static UI/API publishing, Studio login-service installer and docs.
- Verified on a local scratch copy (the workspace is an SMB share): `task check`
  passes — 260 server tests, 80 web tests, 61 TV tests, web typecheck/lint/build.
  Android debug APK builds. Every API route then had contract coverage; generated
  OpenAPI/types verify. Tag and thumbnail routes were added to the contract later;
  `task ci` (`web:rivet:verify`) is the current check. Static publishing succeeds.
- Eight browser journeys pass at a phone viewport against the published .NET
  service, including resume, skip, scrub cancellation and Play Next. TV reports
  are simulated; these tests do not prove hardware playback.
- Installer configuration and shell quoting were exercised with temporary paths
  and a mocked launchctl call. No login service was installed on this computer.
- Deferred: install/run on the Studio with its actual media path and verify the
  physical Fire TV/phone journey. User explicitly requested Android work without
  the device for now. No Fire TV is connected to ADB.

The following is the accepted work scope, retained for reference.

## Shape

One Studio process serves the API and built phone UI. One Fire TV app plays files.
Phone uses the existing mobile web app. SQLite remembers progress and favourites.
Keep direct streaming, existing FFmpeg subtitle/preview support, pairing and HTTP
polling. No new platform, native phone rewrite, cloud service or background worker
framework. No distributed locks, command bus or durable command queues.

Keep the current playback session boundary: phone requests, TV plays and reports.
Keep its basic stale-response and repeated-seek protection. The server's ordinary
in-process lock is small and appropriate; replacing the protocol is not a goal.
Use ExoPlayer, Android lifecycle, standard coroutines and launchd for their jobs.

## What the source says

| Feature | Current position | Required work |
| --- | --- | --- |
| Browse/search/favourites | Implemented | Verify ordinary phone flows |
| Startup scan | Implemented | Check startup with actual media volume |
| Manual rescan | Server endpoint exists; phone refresh only reads library | Wire rescan then refresh catalogue/search |
| Pairing | QR/manual and saved connection exist | Make URLs match the installed service |
| Resume | Saved progress and Resume/Start Over exist | Unify completion rules and verify saving |
| Phone controls | Play/pause/stop, subtitles and thumbnail scrubber exist | Add ±10 seconds, remaining time and verify touch behaviour |
| TV remote | Centre/play-pause and Back implemented | Add simple left/right seeking and visible position |
| Next episode | No current next-episode flow | Offer a conservative next sibling; no queue/autoplay |
| Screen awake | Enabled for entire PlayerScreen lifetime | Release after stop/end/error; check pause behaviour |
| Installed service | Taskfile runs development processes | Publish one service and add login startup |

Concrete rough edges:

- TV ProgressWriter has an unbounded command channel and retries an individual
  failed save indefinitely. Loading another file waits on its flush; disposal
  uses runBlocking. Progress persistence must not hold playback or teardown up.
- TV also has a custom ObservationQueue and several coordination helpers.
  Simplify the I/O path where possible, without deleting useful race protection.
- Completion differs: server watched status and detail resume use 90%, while
  Continue Watching uses 95%.
- Settings labels stored credentials as "Connected", without checking reachability.
- Browser playback E2E simulates TV reports; it does not exercise ExoPlayer.
- Old documentation still describes removed clients and playback features.

## Work order

### 1. Establish a usable baseline

Run the existing server, web and TV checks once; fix actual failures in scope.
Start a real file on the TV early, before polishing. Record device-only problems.
Use the existing tests and fixtures; don't build another test harness.

### 2. Make resume and basic playback dependable

Simplify TV networking around its existing ViewModel/polling lifecycle. Send the
latest observation on the next normal tick after a failure, using bounded request
timeouts; avoid a separate general-purpose retry queue. Keep requests ordered so
old reports cannot overwrite newer state.

Replace the progress command/flush/close machinery with straightforward periodic
and event-driven saves. Capture position before stop, file replacement and release;
save on pause/end/background and about every 10 seconds while playing. A failed
save must not prevent another file loading or block the UI thread. Retry the latest
unsaved position during normal operation with bounded state, not an accumulating
history. Abrupt process death may lose the last interval; no guaranteed-delivery
system or new offline database.

Use the existing 95% Continue Watching cutoff consistently for watched/resume
behaviour, with actual end recorded as complete. Start Over replaces old progress
once playback starts. Refresh phone progress on return to the library/details.

Done when pause/exit/reopen resumes correctly, backwards seeking saves the new
position, file replacement saves the old file, and a temporary failed save cannot
freeze controls. Add focused regression tests for those behaviours.

### 3. Finish ordinary player controls

Phone: add ±10 seconds and remaining time. Keep the existing range input and
thumbnail preview; drag previews locally, release sends one seek, cancel sends
none. Scrubbing must work even if thumbnails fail. Check touch cancellation,
paused seeks and repeated skip taps; do not build a gesture framework.

TV: left/right seeks ±10 seconds with a short position overlay; keep centre and
hardware play/pause, Back stops. Reuse the existing intent path so phone and remote
agree. No second scrubbing state machine. Verify subtitle switching preserves
position and paused/playing state. Tie screen-awake behaviour to playback, releasing
it on end/stop/error; permit normal sleep after an extended pause.

Done when both devices control the same file, seeking is predictable, and loading,
buffering, pause, failure and disconnection are understandable.

### 4. Finish library refresh and episode ending

Wire a visible Refresh Library action to the existing rescan endpoint, then reload
the list. Refresh search results on the next search. No filesystem watcher.
Distinguish saved pairing from actual server reachability in Settings.

Add one next-episode lookup using existing filename parsing and natural ordering:
next recognised episode in the same folder/series. If ambiguous, offer nothing.
Show "Play next: [title]" after completion on phone and TV, using the existing start
operation. No automatic countdown, queue or cross-folder series inference.
Keep the completed-file summary available long enough to use the button, even
after the live server session expires; this is display context, not another session.

### 5. Make it live quietly on the Studio

Publish the Nuxt SPA as static files served by ASP.NET alongside the API, with
client-route fallback. Scope existing key authentication to protected API/media
routes so the phone can load the app before pairing. Retain the LAN restriction
and media-root path guards. No public hosting, tunnel or router changes.

Use one stable LAN address/origin for QR pairing and phone access. Add a small
publish/install script and a per-user launchd agent: start at login, restart on
failure, explicit media/config/FFmpeg paths and ordinary bounded logs. Include
simple start/stop/status/uninstall instructions. Keep the database across updates.
Check media volume availability at login and recover through launchd, without
inventing a mount supervisor. No menu-bar application or admin dashboard.

Test the phone on the actual LAN origin, including a home-screen shortcut where
supported. Do not make service-worker/offline support a requirement for playback;
verify phone behaviour before adding any HTTPS infrastructure.

### 6. Prove the daily journey and tidy documentation

Run relevant automated checks after changes, then the existing aggregate checks.
On the Studio, actual phone and Fire TV: login startup → QR pair → browse/search →
play → pause → skip/scrub → subtitles → exit → resume → finish → play next.
Also try phone reload, server restart, a short network interruption, missing media,
and an unsupported file. Playback failures should be actionable, not stuck spinners.

Replace stale planning documents with a short README covering operation
and the few remaining limitations; update stale web instructions. No new process
documents. Finished means this journey works on the real devices, not just that
the simulated browser test passes.
