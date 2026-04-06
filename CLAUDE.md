# CLAUDE.md — Reel

## Project

Personal, self-hosted media streaming system: Mac server (.NET) + Fire TV client (Kotlin).
See `PLAN.md` for full system design, API spec, and roadmap.

## Structure

Cross-stack colocated repo (not a monorepo). No Turbo/Nx — those solve JS dependency graph
problems that don't exist across .NET + Kotlin.

```
reel/
├── server/          # .NET 9 / ASP.NET Core minimal API (C#)
├── tv/              # Kotlin / Jetpack Compose for TV / ExoPlayer
├── Taskfile.yml     # Language-agnostic task runner
└── PLAN.md          # System design & project plan
```

## Task Runner

Taskfile (https://taskfile.dev) — same pattern as CaseBridge. No magic, language-agnostic.

Key tasks:
- `task server:run` / `task server:build`
- `task tv:build` / `task tv:install` (ADB sideload)
- Add `ci`, `fix`, `check` as project grows

## Server (.NET)

- .NET 9 / ASP.NET Core, minimal API style (no controllers)
- `sealed` on all concrete types
- Records for DTOs and value objects
- Strongly-typed config binding (no raw `process.env` equivalent)
- `Results.File` with `enableRangeProcessing: true` for byte-range streaming
- Path traversal guard: `Path.GetFullPath` + `StartsWith(MediaRoot)`
- MimeTypesMap NuGet for content-type resolution
- EditorConfig for formatting (match CaseBridge conventions)

## TV App (Kotlin)

- Jetpack Compose for TV, Media3/ExoPlayer
- Ktor Client for HTTP
- SharedPreferences for pairing state only
- D-pad navigation throughout
- ExoPlayer header injection via `DefaultHttpDataSource.Factory`

## Formatting & Linting

- **Server**: EditorConfig (same rules as CaseBridge — 4-space indent, file-scoped namespaces,
  `I` prefix interfaces, `_camelCase` private fields, unused code as errors)
- **TV**: Android Studio defaults / ktlint (decide when scaffolding)

## Testing

- **Server**: xUnit + NSubstitute + FluentAssertions (add Testcontainers if persistence grows)
- **TV**: manual end-to-end via ADB for v1 (instrumented tests in v2)

## Deployment (v1)

- Server: `dotnet run` in terminal
- TV: `adb install` sideload
- No CI/CD, no cloud — local network only
- Scotty (https://github.com/spatie/scotty) worth evaluating in v2 for deploy orchestration

## Architecture Principles

Same as global CLAUDE.md — these apply here:
- Data transforms over object hierarchies
- Explicit I/O boundaries, narrow ports
- No magic, no ceremony (no MediatR, no AutoMapper)
- Minimal dependencies
- Readability over abstraction
- Domain depends on nothing; infrastructure implements application ports
