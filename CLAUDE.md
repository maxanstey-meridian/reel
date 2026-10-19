# Reel

Personal LAN media player on one Studio, with one phone remote and one Fire TV.
See README.md for operation and ACTION_PLAN.md for delivery/verification status.

- server/: .NET 10 minimal API, SQLite, direct range streaming (live remux or
  transcode when the TV can't play a file or the phone caps quality) and FFmpeg.
- web/: Nuxt 4 SPA; generated OpenAPI types and openapi-fetch.
- tv/: Kotlin, Compose and Media3/ExoPlayer.
- scripts/: combined publishing and per-user launchd installation.

Keep it small. Reuse platform lifecycle, polling and ordinary storage. No command
bus, distributed locks, accounts, cloud services, queues or generic frameworks.
Phone requests playback; TV reports what actually happened. Preserve the existing
LAN and media-root boundaries. Progress persistence must never block playback.

Use Taskfile commands for checks. API changes require `task web:rivet`.
Tests and builds work best from a local disk, not an SMB-mounted checkout.
Don't reset the media database to update the app. No physical-device verification
claim without actually exercising the TV and phone.
