# Squirrel; notes for Claude sessions

Squirrel is a personal project keeper designed around Yesu's ADHD wiring.
Design principles are non-negotiable and come before feature ideas:

1. Capture must never require more than one line of text and Enter.
2. Every project has exactly ONE next action; never add task lists per project.
3. Focus mode shows one project only; resist adding "just one more panel".
4. Resurfacing is guilt-free by design; copy and UI text must never shame.

## Stack

- .NET 8, C#; Avalonia 11 UI (macOS arm64 / Windows / Linux)
- CommunityToolkit.Mvvm source generators ([ObservableProperty], [RelayCommand])
- SQLite via Microsoft.Data.Sqlite; hand-written store, no EF
- Localhost-only ASP.NET Core minimal API embedded in the desktop app

## Conventions

- Core has zero UI dependencies; App references Core, never the reverse.
- SquirrelStore raises Changed after every write; the UI refreshes from that
  event via Dispatcher.UIThread.Post. Do not add polling.
- Compiled bindings are on by default; templates need x:DataType.
- Communication: no em dashes in copy; use semicolons.

## Verify

`dotnet build -c Release` must pass; CI builds osx-arm64, win-x64, linux-x64.
