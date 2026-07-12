using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Squirrel.App.Api;
using Squirrel.App.Services;
using Squirrel.App.ViewModels;
using Squirrel.App.Views;
using Squirrel.Core;

namespace Squirrel.App;

public class App : Application
{
    private ApiServer? _apiServer;
    private SquirrelStore? _store;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private GlobalHotkey? _hotkey;
    private QuickCaptureWindow? _quickCapture;
    private DispatcherTimer? _nudgeTimer;
    private bool _quitting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _store = new SquirrelStore();
            MainViewModel.ApplyTheme(_store.GetSetting("Theme") ?? "System");
            _apiServer = new ApiServer(_store);
            _viewModel = new MainViewModel(_store, _apiServer.BaseUrl);

            // Start the local capture API in the background; the UI never
            // waits on it, and a port conflict shouldn't kill the app.
            _ = Task.Run(async () =>
            {
                try { await _apiServer.StartAsync(); }
                catch (Exception ex) { Console.Error.WriteLine($"API failed to start: {ex.Message}"); }
            });

            // Quiet background update check; applied on exit if one downloads.
            _ = Task.Run(Updater.CheckAndDownloadAsync);

            _mainWindow = new MainWindow { DataContext = _viewModel };

            // Closing the window hides Squirrel to the tray instead of
            // quitting; captures keep flowing in via the API, and reopening
            // is instant. Quit lives in the tray menu.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _mainWindow.Closing += (_, e) =>
            {
                if (_quitting) return;
                e.Cancel = true;
                _mainWindow.Hide();
            };

            SetupTrayIcon(desktop);
            _store.Changed += () => Dispatcher.UIThread.Post(UpdateTrayTooltip);
            UpdateTrayTooltip();

            // OS-global capture hotkey (Ctrl+Shift+Space). Failure is fine;
            // the tray menu remains the fallback.
            try
            {
                _hotkey = new GlobalHotkey(() => Dispatcher.UIThread.Post(ShowQuickCapture));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Global hotkey unavailable: {ex.Message}");
            }

            // Gentle resurface nudges: first look shortly after launch, then
            // periodic checks; MaybeNudge rate-limits to once per day.
            DispatcherTimer.RunOnce(MaybeNudge, TimeSpan.FromMinutes(2));
            _nudgeTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _nudgeTimer.Tick += (_, _) => MaybeNudge();
            _nudgeTimer.Start();

            desktop.MainWindow = _mainWindow;

            desktop.ShutdownRequested += (_, _) =>
            {
                _hotkey?.Dispose();
                try { _apiServer.StopAsync().GetAwaiter().GetResult(); }
                catch { /* best effort on shutdown */ }
                Updater.ApplyOnExitIfReady();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var openItem = new NativeMenuItem("Open Squirrel");
        openItem.Click += (_, _) => ShowMainWindow();

        var captureItem = new NativeMenuItem("Quick capture…");
        captureItem.Click += (_, _) => ShowQuickCapture();

        var resurfaceItem = new NativeMenuItem("Resurface…");
        resurfaceItem.Click += (_, _) => ShowMainWindow(tabIndex: 3);

        var quitItem = new NativeMenuItem("Quit Squirrel");
        quitItem.Click += (_, _) =>
        {
            _quitting = true;
            _trayIcon?.Dispose();
            desktop.Shutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(openItem);
        menu.Items.Add(captureItem);
        menu.Items.Add(resurfaceItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quitItem);

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(
                new Uri("avares://Squirrel/Assets/squirrel-tray.png"))),
            ToolTipText = "Squirrel",
            Menu = menu
        };

        // Left-click opens the app (Windows/Linux; macOS uses the menu).
        _trayIcon.Clicked += (_, _) => ShowMainWindow();

        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }

    private void ShowMainWindow(int? tabIndex = null)
    {
        if (_mainWindow is null) return;
        if (tabIndex is int i && _viewModel is not null)
            _viewModel.SelectedTabIndex = i;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ShowQuickCapture()
    {
        if (_store is null) return;
        if (_quickCapture is { IsVisible: true })
        {
            _quickCapture.Activate();
            return;
        }
        _quickCapture = new QuickCaptureWindow(_store);
        _quickCapture.Closed += (_, _) => _quickCapture = null;
        _quickCapture.Show();
    }

    private void MaybeNudge()
    {
        if (_store is null) return;

        var stale = _store.GetStaleProjects();
        if (stale.Count == 0) return;

        // At most one nudge per day; a reminder, not a nag.
        var last = _store.GetSetting("LastNudgeAt");
        if (last is not null
            && DateTimeOffset.TryParse(last, out var lastAt)
            && DateTimeOffset.UtcNow - lastAt < TimeSpan.FromHours(20))
            return;

        _store.SetSetting("LastNudgeAt", DateTimeOffset.UtcNow.ToString("O"));

        var message = stale.Count == 1
            ? $"\"{stale[0].Name}\" has been quiet for {stale[0].DaysSinceTouch} days. Want to peek at its next action?"
            : $"{stale.Count} projects are waiting to resurface. Want to peek?";

        new NudgeWindow(message, () => ShowMainWindow(tabIndex: 3)).Show();
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIcon is null || _store is null) return;
        var stale = _store.GetStaleProjects().Count;
        _trayIcon.ToolTipText = stale == 0
            ? "Squirrel"
            : $"Squirrel; {stale} project{(stale == 1 ? "" : "s")} waiting to resurface";
    }
}
