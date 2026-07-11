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
    private bool _quitting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _store = new SquirrelStore();
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

            desktop.MainWindow = _mainWindow;

            desktop.ShutdownRequested += (_, _) =>
            {
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
        captureItem.Click += (_, _) => new QuickCaptureWindow(_store!).Show();

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
                new Uri("avares://Squirrel.App/Assets/squirrel-tray.png"))),
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

    private void UpdateTrayTooltip()
    {
        if (_trayIcon is null || _store is null) return;
        var stale = _store.GetStaleProjects().Count;
        _trayIcon.ToolTipText = stale == 0
            ? "Squirrel"
            : $"Squirrel; {stale} project{(stale == 1 ? "" : "s")} waiting to resurface";
    }
}
