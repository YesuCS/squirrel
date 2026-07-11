# Squirrel 🐿️

A project keeper built for a brain with ADHD wiring: high Wonder and Invention,
great at starting, hyperfocus-prone, and terrible at remembering the seventeen
things already in flight. Squirrel doesn't try to make you a different person;
it works with the wiring.

## The four ideas

**Capture inbox.** A shiny new idea is a squirrel. You don't chase it; you catch
it. The capture bar is always visible at the top of the app and takes one line
of text and Enter. Nothing else is required; no project, no priority, no date.
The idea is safe, and you go back to what you were doing.

**One next action.** Every project stores exactly one next step, sized to be
done in one sitting. Re-entry friction is the thing that kills stalled
projects; when you come back, you never face "where was I?", only one small
concrete instruction from your past self.

**Focus / Now mode.** One project at a time is in focus. The Now tab shows only
that project and its single next action; everything else is out of sight. When
you do the step, you type the next tiny step and swap it in; a momentum loop.

**Resurfacing, without guilt.** Projects you haven't touched in a while show up
in the Resurface tab. The framing is deliberate: pick it back up via its next
action, tell Squirrel it's still alive, or consciously park it. Parking is a
decision, not a failure.

## Capture from anywhere (API)

Squirrel runs a small HTTP API on `http://127.0.0.1:53595` while the app is
open, so you can throw tasks at it from Apple Shortcuts, Alfred, Raycast, a
Stream Deck button, a Rock workflow, or plain curl. The API key is generated on
first run and shown in the Settings tab.

```bash
curl -X POST http://127.0.0.1:53595/capture \
  -H "X-Api-Key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{"text":"idea: squirrel but for sermon clips","source":"curl"}'
```

| Method | Route | Body | Purpose |
| ------ | ----- | ---- | ------- |
| GET  | `/health` | – | Liveness check (no auth) |
| POST | `/capture` | `{text, source?}` | Add to the inbox |
| GET  | `/inbox` | – | Unprocessed inbox items |
| GET  | `/projects` | – | Active and parked projects |
| GET  | `/projects/stale` | – | Projects past the stale threshold |
| POST | `/projects` | `{name, nextAction?, notes?}` | Create a project |
| POST | `/projects/{id}/touch` | – | Mark a project worked-on now |
| POST | `/projects/{id}/next-action` | `{nextAction}` | Set the one next action |

All routes except `/health` require the `X-Api-Key` header. The server binds to
127.0.0.1 only; it is not reachable from the network.

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build            # debug build
dotnet run --project src/Squirrel.App
```

### Self-contained binaries

```bash
./scripts/publish.sh osx-arm64    # Apple Silicon
./scripts/publish.sh win-x64      # Windows (or scripts\publish.cmd)
./scripts/publish.sh linux-x64    # Linux
```

Output lands in `publish/<rid>/`. GitHub Actions (`.github/workflows/build.yml`)
builds all three on every push and uploads the binaries as artifacts.

## Architecture

```
src/
  Squirrel.Core/    domain models + SQLite store (no UI dependencies)
    Models.cs         Project, InboxItem
    SquirrelStore.cs  all persistence; raises Changed after every write
  Squirrel.App/     Avalonia desktop app (macOS arm64 / Windows / Linux)
    Api/ApiServer.cs  localhost capture API, hosted inside the app
    ViewModels/       MVVM via CommunityToolkit.Mvvm
    Views/            MainWindow: Now / Inbox / Projects / Resurface / Settings
```

Data is one SQLite file (path shown in Settings; typically under your
platform's application-data folder in `Squirrel/squirrel.db`). Back it up by
copying the file.

## Roadmap ideas

- System tray / menu bar presence with a global capture hotkey
- Gentle nudge notifications when the Resurface list grows
- Streaks and small wins log (dopamine matters)
- Optional sync between machines
- Auth token per client instead of a single shared key
