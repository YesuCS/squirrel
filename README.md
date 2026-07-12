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

## Global hotkey and nudges

Ctrl+Shift+Space summons quick capture from anywhere, whatever app has focus
(macOS needs Accessibility permission; most Wayland sessions block global
hooks, so use the tray there). When projects go quiet past the stale
threshold, Squirrel shows one small corner nudge at most once per day; it
never steals focus and dismisses itself.

## Priorities, due dates, and the Now suggestion

Every project carries a required priority (1-10, default 5) and an optional
due date. An urgency score (priority + due-date pressure + a small neglect
nudge) orders the Projects tab, and when nothing is in focus the Now tab
offers the top candidate instead of asking you to choose: "Let's do this",
"Something else", or "I'll pick myself".

## Manual

The full user manual ships inside the app (Settings > Open the manual) and
lives at `src/Squirrel.App/Assets/MANUAL.md`.

## macOS .app

CI builds a proper `Squirrel.app` bundle (with the .icns icon) via Velopack.
To build one locally on a Mac: `dotnet tool install -g vpk` once, then
`./scripts/make-app.sh`; the bundle lands in `./Releases`.

## Living in the tray

Closing the window doesn't quit Squirrel; it tucks into the system tray (menu
bar on macOS) and keeps accepting API captures. The tray menu has Open, Quick
capture (a tiny always-on-top box; Enter saves, Esc closes), Resurface (jumps
straight to that tab), and Quit. The tray tooltip shows how many projects are
waiting to resurface.

## Auto-updates

Installed builds check GitHub Releases quietly on launch via
[Velopack](https://velopack.io); if a new version exists it downloads in the
background and applies on exit, never mid-session. Set your repo URL in
`src/Squirrel.App/Services/Updater.cs` (`Updater.RepoUrl`) before your first
release. Running from source skips all of this automatically.

## Releasing

Tag a version and push it; CI does the rest:

```bash
git tag v1.0.0
git push origin v1.0.0
```

`.github/workflows/release.yml` publishes self-contained builds for
osx-arm64, win-x64, and linux-x64, packs installers with Velopack, and
uploads everything to a GitHub Release. Existing installs auto-update from
there. Note: macOS builds are unsigned; first launch needs right-click →
Open (code signing/notarization requires an Apple Developer account and can
be added to the workflow later).

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
