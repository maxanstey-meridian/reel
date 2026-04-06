# Reel - System Design & Project Plan

*Working title. A personal, self-hosted media streaming system: Mac → Fire TV.*

---

## 1. Project Overview

### Purpose

Reel is a personal, self-hosted media streaming system that lets you browse and

play your Mac's media library directly from a Fire TV Stick, via a custom native

Android TV application. No cloud. No subscriptions. No third-party servers. No

unnecessary complexity.

### Core Problem Being Solved

Existing solutions (Jellyfin, Plex) are unreliable on macOS, suffer from opaque

transcoding failures, and push UI complexity to the server side. Reel takes a

different approach: the TV is the primary interface, the server is dumb

infrastructure, and v1 scope is deliberately minimal so that what ships actually

works.

### Design Philosophy

- **Client-first UX** - You browse and play from the TV. The server serves, it

  does not orchestrate.

- **Server is dumb** - All UI intelligence lives in the TV app. The server

  indexes, authenticates, and streams.

- **Direct play by default** - No server-side transcoding in v1. Send raw bytes,

  let ExoPlayer handle decoding.

- **No magic** - Transparent behaviour. If something fails, you know why.

- **Scope discipline** - v1 does a small set of things correctly. Features are

  earned in v2+.

---

## 2. System Architecture

~~~

┌──────────────────────────────┐        ┌──────────────────────────────┐

│         Mac Server           │        │        Fire TV App           │

│   (.NET / ASP.NET Core)      │◄───────│  (Kotlin / Compose for TV)   │

│                              │  HTTP  │                              │

│  - Indexes media root dir    │  REST  │  - Directory tree browser    │

│  - Validates secret key      │   +    │  - Pairing screen (once)     │

│  - Serves raw file bytes     │ Range  │  - Playback via ExoPlayer    │

│  - Exposes REST API          │        │  - Stores {ip, key} locally  │

└──────────────────────────────┘        └──────────────────────────────┘

          │

    Local filesystem

    (your media files)

~~~

### Data Flow (playing a file)

1. TV app fetches library tree via `GET /api/library`

2. User navigates tree, selects a file, hits play

3. App constructs stream URL:

   `http://{serverIp}:{port}/api/files/stream?path={encodedPath}`

4. ExoPlayer opens the URL with the secret key header attached

5. Server validates the key, opens the file, streams bytes via HTTP byte-range

6. ExoPlayer decodes and plays - seeking works for free via byte-range

---

## 3. Hardware & Environment

| Component  | Spec                                          |

|------------|-----------------------------------------------|

| Server     | MacBook Pro (macOS, Apple Silicon or Intel)   |

| Client     | Amazon Fire TV Stick 4K                       |

| Network    | Local WiFi (both devices on same network)     |

| TV         | LG webOS (Fire TV Stick plugged into HDMI)    |

### Why Fire TV Stick (not Chromecast)

Google has been progressively tightening sideloading restrictions on Google TV

through 2025-2026 - the long-term trajectory is hostile to custom APKs. Amazon,

by contrast, is structurally incentivised to keep sideloading open (they use it

themselves for non-Play-Store apps). The Fire TV Stick 4K (~£35-45) is the

right foundation.

### Developer Mode & Sideloading Setup

1. Settings → My Fire TV → About → tap **Build Number** 7 times

2. Settings → My Fire TV → Developer Options → **ADB Debugging: ON**

3. Settings → My Fire TV → Developer Options → **Apps from Unknown Sources: ON**

4. On Mac: `adb connect {fire_tv_ip}:5555`

5. `adb install reel.apk`

Fire TV IP: Settings → My Fire TV → About → Network

---

## 4. Security & Pairing Model

### Philosophy

Single-user, local network only. The threat model is "someone on your WiFi."

A static shared secret is sufficient. No sessions, no tokens, no database.

### Secret Key

- Defined in `appsettings.json` on first setup

- A short human-readable string is fine: 

Continuing - picking up in Section 4 where the cut happened:

markdown

  `REEL-7F2K` format is perfectly readable
- Printed to stdout on server startup alongside the server's local IP
- Never changes unless manually regenerated in config

### Initial Pairing Flow

~~~
Server starts → prints to stdout:
  Reel running at http://192.168.1.100:8080
  Secret key:    REEL-7F2K

User opens Fire TV app (first launch only):
  → Sees pairing screen
  → Enters server IP: 192.168.1.100
  → Enters secret key: REEL-7F2K
  → Taps Connect

App sends:
  POST /api/connect
  Body: { "key": "REEL-7F2K" }

Server validates → returns:
  { "status": "ok", "serverName": "Max's Mac" }

App stores { ip, port, key } in SharedPreferences
Subsequent launches: auto-connects, pairing screen never shown again
~~~

### Per-Request Validation
All endpoints except `/api/connect` and `/api/health` require:

~~~
X-Secret-Key: REEL-7F2K
~~~

Validated by a single ASP.NET Core middleware. Returns `401 Unauthorized`
on mismatch. One place, one responsibility.

---

## 5. Server Specification

### 5.1 Responsibilities
- Read media root directory path from config
- Walk the directory tree recursively and expose it as JSON
- Serve file bytes with HTTP byte-range support (seeking for free)
- Validate secret key on every protected request
- Print server IP and secret key to stdout on startup

### 5.2 Configuration (`appsettings.json`)

~~~json
{
  "Reel": {
    "MediaRoot": "/Users/yourname/Media",
    "SecretKey": "REEL-7F2K",
    "Port": 8080
  }
}
~~~

### 5.3 API Reference

#### `POST /api/connect`
Auth: none required
Body: `{ "key": "string" }`
Response 200: `{ "status": "ok", "serverName": "string" }`
Response 401: `{ "status": "unauthorized" }`

#### `GET /api/health`
Auth: none
Response 200: `{ "status": "healthy", "uptime": "string" }`

#### `GET /api/library`
Auth: `X-Secret-Key` header required
Response 200: Recursive `FileNode` tree rooted at `MediaRoot`

#### `GET /api/files/stream?path={relativePath}`
Auth: `X-Secret-Key` header required
Supports: `Range` header (HTTP 206 Partial Content)
Returns: Raw file bytes with correct `Content-Type`
`relativePath` is URL-encoded, relative to `MediaRoot`

### 5.4 FileNode Model

~~~json
{
  "name": "The Thing (1982).mkv",
  "type": "file",
  "relativePath": "Films/The Thing (1982).mkv",
  "extension": ".mkv",
  "sizeBytes": 12483942400,
  "children": null
}
~~~

Directories: `"type": "directory"`, `"children": [...]`,
no `sizeBytes` or `extension`.

### 5.5 File Serving
ASP.NET Core's `Results.File` with `enableRangeProcessing: true` handles
HTTP 206 Partial Content natively. Seeking in ExoPlayer requires no extra
server-side work.

~~~csharp
// StreamEndpoints.cs - conceptual shape
app.MapGet("/api/files/stream", (string path, ReelConfig config) =>
{
    var fullPath = Path.GetFullPath(
        Path.Combine(config.MediaRoot, path)
    );

    // Security: ensure resolved path is still under MediaRoot
    if (!fullPath.StartsWith(config.MediaRoot))
        return Results.Forbid();

    var mime = MimeTypeMap.GetMimeType(Path.GetExtension(path));
    return Results.File(fullPath, mime, enableRangeProcessing: true);
});
~~~

### 5.6 Content-Type Mapping
Key MIME types to cover:

| Extension     | MIME Type               |
|---------------|-------------------------|
| `.mp4`        | `video/mp4`             |
| `.mkv`        | `video/x-matroska`      |
| `.avi`        | `video/x-msvideo`       |
| `.mov`        | `video/quicktime`       |
| `.webm`       | `video/webm`            |
| `.mp3`        | `audio/mpeg`            |
| `.flac`       | `audio/flac`            |
| `.aac`        | `audio/aac`             |
| `.m4a`        | `audio/mp4`             |

Use the `MimeTypesMap` NuGet package rather than rolling your own.

### 5.7 Path Traversal Guard
Always resolve the requested path with `Path.GetFullPath` and assert it
starts with `MediaRoot` before opening the file. One line of protection
against `../../../etc/passwd` style requests.

### 5.8 macOS Service (v1 vs v2)
- **v1**: Console app. Run manually in terminal. `dotnet run` or a simple
  shell script.
- **v2**: macOS LaunchAgent plist for auto-start on login, plus a
  `NSStatusItem` menubar icon (small Swift wrapper or Avalonia UI) showing
  server status and the current secret key.

---

## 6. Client Specification

### 6.1 Responsibilities
- Show pairing screen on first launch only
- Store and retrieve server connection details locally
- Fetch and render the file/directory tree
- Navigate the tree with D-pad/remote
- Initiate playback via ExoPlayer with auth header injected
- Handle play and pause (seek available for free via byte-range)

### 6.2 Local Storage Schema (SharedPreferences)

~~~
reel_server_ip    →  "192.168.1.100"
reel_server_port  →  "8080"
reel_secret_key   →  "REEL-7F2K"
reel_paired       →  "true"
~~~

On launch: if `reel_paired == true`, skip pairing and go straight to
library. On connection failure, surface an error and offer to re-pair.

### 6.3 Screens

#### Pairing Screen (first launch only)
- Text field: Server IP address
- Text field: Secret key
- "Connect" button
- On submit: `POST /api/connect` with the entered key
- On 200: store prefs, set `reel_paired = true`, navigate to Library
- On 401/failure: show inline error, stay on screen

#### Library Screen
- Fetches `GET /api/library` on mount
- Renders the `FileNode` tree: folders and files as a navigable list
- Folders: selecting them pushes a new screen (or expands in-place) showing
  their children
- Files: selecting them navigates to Playback Screen passing `relativePath`
- D-pad fully navigable, Back button traverses up the tree
- Show file size next to filename for context

#### Playback Screen
- Full-screen ExoPlayer surface
- Constructs URL: `http://{ip}:{port}/api/files/stream?path={encodedPath}`
- Injects secret key via custom HTTP data source (see 6.4)
- Transport: Play / Pause (ExoPlayer built-in overlay)
- Seeking: available for free via byte-range, surface it in the controls
- Back button stops playback, releases player, returns to Library

### 6.4 ExoPlayer Header Injection

~~~kotlin
val dataSourceFactory = DefaultHttpDataSource.Factory()
    .setDefaultRequestProperties(
        mapOf("X-Secret-Key" to preferencesManager.secretKey)
    )

val mediaItem = MediaItem.fromUri(streamUrl)

val mediaSource = ProgressiveMediaSource.Factory(dataSourceFactory)
    .createMediaSource(mediaItem)

player.setMediaSource(mediaSource)
player.prepare()
player.play()
~~~

### 6.5 Network Error Handling (v1 minimum)
- Connection timeout: show "Can't reach server" with a Retry button
- 401 response: show "Invalid key - please re-pair" and clear stored prefs
- File not found (404): show "File unavailable" and return to library
- No silent failures - always surface what went wrong

---

## 7. Streaming Model

### Direct Play (v1)
Server sends raw file bytes. Client decodes entirely on-device. Server
processing cost: zero.

Works natively in ExoPlayer on Fire TV Stick 4K:

| Format             | Status                         |
|--------------------|--------------------------------|
| MP4 / H.264 / AAC  | Works                          |
| MP4 / H.265 / AAC  | Works (Fire TV 4K has HW dec)  |
| WebM / VP9         | Works                          |
| MKV / H.264        | Partial (ExoPlayer hit-or-miss)|
| MKV / H.265        | Partial                        |
| AVI                | Often fails                    |

If a file doesn't play in v1, note the format and that informs v2 remux
priority. Don't pre-optimise.

### Remux / Direct Stream (v2)
Server re-wraps the container without re-encoding the video or audio
stream. MKV → MP4 wrapper, H.264 inside stays untouched. Very cheap.
Implemented via FFmpeg piped to the response stream.

~~~
ffmpeg -i input.mkv -c:v copy -c:a copy -f mp4 -movflags frag_keyframe+empty_moov pipe:1
~~~

### Transcode (v3)
Server re-encodes video and/or audio into a compatible codec. CPU/GPU
intensive. Uses VideoToolbox on macOS for hardware-accelerated H.264/H.265
output. This is the hard problem - don't touch it until v2 remux is stable.

**Decision order at play-time (v2+):**
1. Can client play the source codec and container directly? → Direct play
2. Is only the container wrong? → Remux
3. Is the codec incompatible? → Transcode

---

 Syntax highlighting is disabled for large code blocks

Continuing with the final sections in the next message.

markdown

## 8. v1 Scope

### In Scope
- Mac server: directory indexing, secret key auth middleware, file streaming
  with byte-range support
- Fire TV app: pairing screen, directory tree browser (D-pad navigable),
  direct play via ExoPlayer with auth header
- Play, pause, and seeking (free via ExoPlayer + byte-range)
- Single user, no profiles, no accounts
- Local network only

### Explicitly Out of Scope (v1)
- Transcoding or remuxing of any kind
- Metadata enrichment (no TMDB, no thumbnails, no poster art)
- Watch progress persistence / resume from position
- Playlists or play queues
- Subtitle rendering
- Search or filtering
- macOS menubar icon or LaunchAgent auto-start
- Multiple media root directories
- Remote / external access (no port forwarding, no DDNS, no tunnels)

---

## 9. Tech Stack

| Layer              | Technology                     | Notes                              |
|--------------------|--------------------------------|------------------------------------|
| Server runtime     | .NET 9 / ASP.NET Core          | Minimal API style, no controllers  |
| Server language    | C# 13                          |                                    |
| Server host        | macOS (console app, v1)        | LaunchAgent in v2                  |
| TV client language | Kotlin                         |                                    |
| TV UI framework    | Jetpack Compose for TV         | Official Google/Android TV path    |
| TV playback        | Media3 / ExoPlayer             | HTTP, byte-range, seek built-in    |
| TV target hardware | Amazon Fire TV Stick 4K        | Fire OS (Android fork), API 28+    |
| HTTP client (TV)   | Ktor Client                    | Kotlin-native, coroutine-friendly  |
| Local storage (TV) | SharedPreferences              | Pairing info only                  |
| Sideloading        | ADB over WiFi                  | Dev mode on Fire TV                |
| MIME types (.NET)  | MimeTypesMap NuGet package     |                                    |

---

## 10. Project Structure

### Server

~~~
Reel.Server/
├── Program.cs                   ← Minimal API setup, middleware, startup log
├── appsettings.json             ← MediaRoot, SecretKey, Port
├── appsettings.Development.json ← Local overrides (gitignored)
├── Middleware/
│   └── SecretKeyMiddleware.cs   ← Validates X-Secret-Key on all protected routes
├── Endpoints/
│   ├── ConnectEndpoints.cs      ← POST /api/connect
│   ├── LibraryEndpoints.cs      ← GET /api/library
│   └── StreamEndpoints.cs       ← GET /api/files/stream (+ path traversal guard)
├── Services/
│   ├── LibraryService.cs        ← Recursive directory walk → FileNode tree
│   └── ReelConfig.cs            ← Strongly-typed config binding
└── Models/
    ├── FileNode.cs              ← Directory/file tree node (record type)
    └── ConnectRequest.cs        ← POST /api/connect body
~~~

### TV App

~~~
app/src/main/java/com/max/reel/
├── MainActivity.kt              ← Nav host, entry point, paired/unpaired routing
├── data/
│   ├── PreferencesManager.kt   ← SharedPreferences read/write wrapper
│   └── ApiClient.kt            ← Ktor client, base URL + X-Secret-Key header
├── ui/
│   ├── pairing/
│   │   └── PairingScreen.kt    ← IP + key entry, POST /api/connect
│   ├── library/
│   │   └── LibraryScreen.kt    ← FileNode tree browser, D-pad nav
│   └── player/
│       └── PlayerScreen.kt     ← ExoPlayer full-screen, play/pause/seek
└── model/
    └── FileNode.kt             ← Data class mirroring server model
~~~

---

## 11. Development Roadmap

### v1 - It Actually Works
*Goal: stream any supported file from Mac to Fire TV with a working library
browser. Everything in this list is in scope, nothing else is.*

**Server**
- [ ] Project scaffold: solution, minimal API, config binding
- [ ] `SecretKeyMiddleware` - validates header, returns 401
- [ ] `POST /api/connect` - key validation, server name response
- [ ] `GET /api/health` - no auth, basic liveness
- [ ] `LibraryService` - recursive dir walk, FileNode tree
- [ ] `GET /api/library` - calls LibraryService, returns JSON tree
- [ ] `GET /api/files/stream` - byte-range file serving, path traversal guard,
  MIME type mapping
- [ ] Startup log: print local IP and secret key to stdout

**TV App**
- [ ] Android TV project scaffold, Compose for TV, Media3 dependencies
- [ ] `PreferencesManager` - read/write pairing prefs
- [ ] `ApiClient` - Ktor client with base URL + header injection
- [ ] `PairingScreen` - IP/key input, connect flow, error states
- [ ] `LibraryScreen` - fetch tree, render dirs and files, D-pad navigation
- [ ] `PlayerScreen` - ExoPlayer setup, header injection, play/pause/seek
- [ ] End-to-end sideload test via ADB and play a real file

### v2 - Actually Usable Daily
- Resume from position (SQLite on server, progress reported from app)
- macOS LaunchAgent + plist for auto-start on login
- Server menubar status icon (shows IP and key at a glance)
- Remux support via FFmpeg (MKV → fragmented MP4 pipe) for problem formats
- Poster grid view in library (thumbnail extraction or TMDB enrichment)
- Display-friendly filenames (strip release year, dots, codec tags)

### v3 - Power Features
- Transcoding pipeline (VideoToolbox HW acceleration on Mac)
- Subtitle support (SRT/ASS extraction and delivery to ExoPlayer)
- Search and filter in TV app
- Multiple media root directories (e.g. separate Movies and TV Shows roots)
- Playlist / continuous play queue

---

## 12. Getting Started

### Prerequisites
- .NET 9 SDK → https://dotnet.microsoft.com/download
- Android Studio (Ladybug or later) → https://developer.android.com/studio
- ADB: `brew install android-platform-tools`
- Amazon Fire TV Stick 4K with developer mode enabled (see Section 3)

### Step 1: Bootstrap the Server

~~~bash
mkdir Reel && cd Reel
dotnet new sln -n Reel
dotnet new webapi -n Reel.Server --use-minimal-apis --no-openapi
dotnet sln add Reel.Server/Reel.Server.csproj
cd Reel.Server
dotnet run
~~~

Edit `appsettings.json` before running:

~~~json
{
  "Reel": {
    "MediaRoot": "/Users/yourname/Media",
    "SecretKey": "REEL-XXXX",
    "Port": 8080
  }
}
~~~

### Step 2: Bootstrap the TV App
1. Open Android Studio → New Project → **TV → Empty Compose for TV Activity**
2. Package: `com.yourname.reel`
3. Minimum SDK: API 25 (Fire TV Stick 4K runs API 28)
4. Add to `build.gradle.kts` (app module):

~~~kotlin
// Media3 / ExoPlayer
implementation("androidx.media3:media3-exoplayer:1.4.1")
implementation("androidx.media3:media3-ui:1.4.1")

// Compose for TV
implementation("androidx.tv:tv-foundation:1.0.0")
implementation("androidx.tv:tv-material:1.0.0")

// Ktor HTTP client
implementation("io.ktor:ktor-client-android:2.3.12")
implementation("io.ktor:ktor-client-content-negotiation:2.3.12")
implementation("io.ktor:ktor-serialization-kotlinx-json:2.3.12")

// Kotlinx serialization (for FileNode JSON)
implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.6.3")
~~~

### Step 3: Connect Fire TV via ADB

~~~bash
# Find Fire TV IP: Settings → My Fire TV → About → Network
adb connect 192.168.1.XXX:5555
adb devices   # verify it appears in list
~~~

### Step 4: Sideload the App

~~~bash
# Run directly from Android Studio to the connected device, or:
adb install app/build/outputs/apk/debug/app-debug.apk
~~~

### Step 5: First Run
1. Start the server: `dotnet run` - note the IP and key printed to console
2. Open the app on Fire TV - pairing screen appears
3. Enter the IP and key, tap Connect
4. Library screen loads - navigate to a file, hit play

---

*Stack: .NET 9 / ASP.NET Core (server) · Kotlin / Compose for TV / ExoPlayer
(client)*
*Hardware: MacBook Pro + Amazon Fire TV Stick 4K via ADB sideload*
*Scope: personal, single-user, local network only*
*Working title: Reel*
