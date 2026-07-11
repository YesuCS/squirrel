SQUIRREL USER MANUAL


WHAT SQUIRREL IS

Squirrel is a project keeper built for a brain that starts things brilliantly,
hyperfocuses hard, and then genuinely forgets what else was in flight. It is
not a to-do list and it will never show you a wall of overdue tasks. It rests
on four ideas:

1. Catch every shiny idea instantly, with zero required fields.
2. Every project has exactly one next action, never a list.
3. Only one project is "Now" at a time.
4. Untouched projects resurface gently, without guilt.


CATCHING IDEAS

The capture bar sits at the top of the main window at all times. Type the
idea, press Enter, done. It lands in the Inbox and asks nothing more of you.

Quick capture works from anywhere, even when Squirrel is hidden:

* Press Ctrl+Shift+Space (the global hotkey), or
* Right-click the tray icon and choose "Quick capture".

A small box appears; Enter saves, Esc closes. Either way you are back in your
previous app in about two seconds, and the idea is safe.

You can also capture over HTTP; see SENDING TASKS FROM ANYWHERE below.


THE TABS

NOW is home. It shows the one project you chose to focus on and its single
next action, nothing else. When you finish the step, type the next tiny step
in the box and press "Done, swap it in". That keeps a fresh re-entry point
waiting for you at all times. "I worked on this" records progress without
changing the step. "Clear focus" steps away without judgment.

INBOX holds everything you have caught. When you have a spare moment (not
right when you capture), triage: "→ Project" turns an idea into a real
project; "Dismiss" lets it go. An idea that stops being exciting was still
worth catching.

PROJECTS lists everything active or parked. Each card shows the project's one
next action (editable; Save updates it), how long since you touched it, and
buttons: Focus makes it your Now; Touch says "still alive"; Park shelves it
on purpose; Done celebrates and archives it; Delete removes it entirely.

RESURFACE shows active projects that have gone quiet past your threshold
(default 7 days, adjustable in Settings). This is not a list of failures; it
is your past self waving hello. For each one: "Focus on this" re-enters
through the next action, "Still alive" resets the clock, "Park it,
guilt-free" shelves it consciously. Parking is a decision, not a defeat.

SETTINGS holds the API details, the stale-day threshold, and the location of
your data file.

MANUAL is this document.


ONE NEXT ACTION, THE RULE THAT MATTERS MOST

When you set a next action, make it small and physical: "open MainWindow.axaml
and rename the tab", not "finish the UI". The test: could you start it within
ten seconds of sitting down? If not, cut it in half. Squirrel enforces one
next action per project on purpose; a list of next actions is just another
overwhelming backlog.


SENDING TASKS FROM ANYWHERE (THE API)

While Squirrel runs (even hidden in the tray), it listens on
http://127.0.0.1:53595. Every route except /health requires your API key in
the X-Api-Key header; the key is in Settings.

Capture example:

    curl -X POST http://127.0.0.1:53595/capture \
      -H "X-Api-Key: YOUR_KEY" \
      -H "Content-Type: application/json" \
      -d '{"text":"idea from the terminal","source":"curl"}'

Routes:

    GET  /health                      liveness, no auth
    POST /capture                     {text, source?} -> inbox
    GET  /inbox                       unprocessed items
    GET  /projects                    active and parked projects
    GET  /projects/stale              projects past the threshold
    POST /projects                    {name, nextAction?, notes?}
    POST /projects/{id}/touch         mark worked-on now
    POST /projects/{id}/next-action   {nextAction}

Apple Shortcuts recipe: new shortcut, "Get Contents of URL", method POST, URL
http://127.0.0.1:53595/capture, headers X-Api-Key and Content-Type
(application/json), request body JSON with a "text" field using Ask Each
Time. Note that this reaches Squirrel only on the same machine; phone capture
arrives with the cloud sync stage.

The API binds to 127.0.0.1 only; nothing on your network can reach it.


THE TRAY, NUDGES, AND QUITTING

Closing the window hides Squirrel to the tray (menu bar on macOS); capture
keeps working. The tray menu has Open, Quick capture, Resurface, and Quit;
the tooltip shows how many projects are waiting.

When projects are waiting to resurface, Squirrel shows one small nudge in the
corner of your screen at most once per day. It never steals focus and it
dismisses itself after fifteen seconds. "Later" is always a fine answer.


UPDATES

Installed builds check for updates quietly at launch and apply them when you
quit, never mid-session. Running from source skips updates entirely.


YOUR DATA

Everything lives in one SQLite file; the exact path is shown in Settings
(a Squirrel folder inside your platform's application-data directory).
Back up by copying that file anywhere. There is no cloud copy unless you
build one; your ideas stay on your machine.


TROUBLESHOOTING

Global hotkey does nothing on macOS: grant Accessibility permission in
System Settings > Privacy & Security > Accessibility, then restart Squirrel.

Global hotkey does nothing on Linux: global hooks work on X11; most Wayland
sessions block them by design. Use the tray menu's Quick capture instead.

macOS says the app can't be opened: current builds are unsigned; right-click
the app and choose Open the first time.

API returns 401: the X-Api-Key header is missing or doesn't match the key in
Settings.

API not responding: another process may be using port 53595; quit Squirrel
fully (tray > Quit) and relaunch.

Window is gone but Squirrel still runs: that's hide-to-tray; click the tray
icon or its Open item.


A NOTE FROM THE DESIGN

If you came back after weeks away: welcome back. Nothing here is overdue,
because nothing here has due dates. Open Resurface, pick one thing or park
things freely, and set one tiny next action. That's the whole method.
