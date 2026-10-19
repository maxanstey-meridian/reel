# Reel

A personal LAN media player: one Studio service, a phone web app and a Fire TV app.
The phone browses and controls; the TV plays; SQLite remembers progress/favourites.

## Daily use

Open Reel on the TV and pair with the Studio. Scan its QR code on your phone.
Bookmark the phone page or add it to your home screen if your browser supports it.
Browse folders, search, favourite files, or pick Continue Watching. Tag files and
search with `#tag`; untagged items count as `#media`. Video cards show thumbnails
generated on first view (cached in `.reel`). Choose Resume or Start Over. Now
Playing has play/pause, stop, ±10 seconds, thumbnail scrubbing, remaining time
and subtitles. The TV remote supports play/pause, left/right skips and Back to
stop. A completed episode offers Play Next when the next SxxExx file is
unambiguous in the same folder. No autoplay or queue.

Pull down to reload what's on screen; use Look for new files on Library or Search to rescan. Progress saves every 10 seconds
and on pause/stop/end/exit; abrupt shutdown can lose the last interval. At 95% a
file is treated as watched. A stopped/restarted server loses its live session;
choose Resume to start again. Progress and favourites survive server restarts.

## Install on the Studio

Requires .NET 10, Node/pnpm and FFmpeg (including ffprobe). Build on a local disk,
not through an SMB-mounted checkout. Give the Studio a stable LAN hostname/address.
From this repository on the Studio:

```sh
python3 scripts/service.py install --media "/actual/media/folder" --url "http://studio.local:8080"
```

Use your actual media folder and reachable Studio hostname. This builds the phone
UI and server, installs them under `~/Library/Application Support/Reel`, and starts
one per-user launchd service at login. Install again to update; its key, database
and favourites are retained. launchd starts the server whenever the media folder
is present and leaves it stopped while the volume is missing. It runs while the Studio is awake and the
user is logged in; no wake-on-LAN or sleep management is included.

```sh
python3 scripts/service.py status
python3 scripts/service.py stop
python3 scripts/service.py start
python3 scripts/service.py uninstall
```

Uninstall removes login startup, retaining config/media data. Configuration is in
`~/Library/Application Support/Reel/appsettings.json`. The server reads that file
directly. Install sets the media folder and URL from its arguments and keeps every
other setting (including hand edits and the key).

Streaming: a file the Fire TV plays is served as-is with byte ranges. One it can't is
converted live, touching as little as possible: playable video in another container
(AVI, WMV, FLV) or with unplayable audio (DTS, TrueHD, MP2) keeps its video, and its
audio too if the TV plays it, otherwise it gets AC3 audio (up to 5.1); unplayable
video (WMV, 10-bit H.264) is transcoded to H.264 at its own height. On weak wifi pick a lower Quality (1080p, 720p, 480p) on the phone's
Now Playing screen: video taller than that is transcoded down to it. Audio files are
never converted. A converted stream seeks by reloading, and image-based subtitles are
not offered with it.

Quality and subtitles chosen while something plays are remembered in the media
database: the next file starts at that quality, with subtitles in the language last
picked (or off). Languages are matched however a track names them: `eng`, `English`,
`en-US`, `.en.srt`, RARBG's `Subs/<episode>/7_English.srt`. Subtitle files loose in a
`Subs/` folder go to the video they're named after; one named only by language
(`Subs/2_English.srt`) goes to a video only when it's the only one in its folder. With
no track in that language, a sidecar that names no language is used. The `Reel`
settings are read at startup: `stop` then `start` after changing them. Runtime
messages use macOS logging (`log show --last 1h --predicate 'process == "logger"'`).
Media index, progress and generated assets live in the media folder's `.reel`
directory. Do not delete that folder to update the app.

The same LAN origin serves both UI and API. The app shell loads before pairing.
The key is broadcast over mDNS for TV auto-pairing and shown in the pairing QR
code, so it is not a secret on your network: the real boundaries are the
private-network restriction and media-root path checks. No public hosting,
tunnels, accounts or router configuration. Ordinary LAN HTTP works for the phone
UI; offline/service-worker installation is not needed. The list of what plays as-is
(`server/Reel.Server/Media/Transcoding.cs`) is for a Fire TV Stick 4K Max.

### If the UI shows skeletons forever

Run `python3 scripts/service.py status`. If it reports hung or unavailable and the
media drive is external, look for a macOS prompt allowing access to files on a
removable volume and click Allow. A process stuck on the drive may ignore `stop`;
end it with `kill -9 PID` and launchd restarts it.

## TV installation

With an Android SDK and Java 17 configured (`tv/local.properties`), run:

```sh
adb connect TV_LAN_ADDRESS:5555
task tv:install
```

`tv:install` builds and installs the shrunk release APK (signed with the debug key);
debug builds run noticeably slower on Fire TV. Enable ADB debugging on the TV first. Android Studio itself is optional for builds.

## Development

Run `dotnet tool restore` once after cloning; it installs the pinned CSharpier and Rivet tools from
`.config/dotnet-tools.json`. `task dev` starts the .NET API (:8080) and Nuxt dev server (:3000). Supply real
`Reel__MediaRoot`, `Reel__SecretKey` and `Reel__WebUrl` (all required; `Reel__Port` defaults to 8080) as environment
values or in the git-ignored `server/Reel.Server/appsettings.Development.json`. `task publish` builds the combined app
into `dist/`. `task check` runs server, web and TV checks; `task ci` additionally
verifies generated contracts and runs browser journeys. To test the combined build,
run `E2E_PUBLISHED_DIR=/absolute/path/to/dist pnpm --dir web e2e`. Browser E2E uses simulated
TV observations, so it cannot certify physical playback or remote behaviour.

API changes: `task web:rivet` regenerates OpenAPI and TypeScript types. Web composables
call the openapi-fetch `client` from `web/app/lib/api.ts`, whose middleware owns pairing
auth, timeouts and errors. Player ownership is in TV PlaybackViewModel;
the server has a single in-memory PlaybackSessionService. Keep those boundaries
small; use standard platform facilities instead of adding coordination frameworks.
