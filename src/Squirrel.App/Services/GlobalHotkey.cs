using SharpHook;
using SharpHook.Native;

namespace Squirrel.App.Services;

/// <summary>
/// OS-global capture hotkey via SharpHook (libuiohook): Ctrl+Shift+Space
/// from anywhere summons quick capture, no matter which app has focus.
///
/// Platform notes:
/// - macOS requires Accessibility permission (System Settings > Privacy
///   &amp; Security > Accessibility); without it the hook simply stays silent.
/// - Linux works on X11; on Wayland global hooks are restricted by design
///   and may not fire. The tray menu remains the fallback everywhere.
/// The callback is raised on a background thread; marshal to the UI thread
/// before touching any window.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private readonly TaskPoolGlobalHook _hook = new();
    private readonly Action _callback;

    public GlobalHotkey(Action callback)
    {
        _callback = callback;
        _hook.KeyPressed += OnKeyPressed;

        // Fire and forget; if the hook can't start (permissions, Wayland),
        // the hotkey is unavailable but the app is unaffected.
        _ = _hook.RunAsync();
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode != KeyCode.VcSpace)
            return;

        var mask = e.RawEvent.Mask;
        var ctrl = (mask & (ModifierMask.LeftCtrl | ModifierMask.RightCtrl)) != ModifierMask.None;
        var shift = (mask & (ModifierMask.LeftShift | ModifierMask.RightShift)) != ModifierMask.None;

        if (ctrl && shift)
            _callback();
    }

    public void Dispose()
    {
        try
        {
            _hook.KeyPressed -= OnKeyPressed;
            _hook.Dispose();
        }
        catch
        {
            // Hook teardown is best effort.
        }
    }
}
