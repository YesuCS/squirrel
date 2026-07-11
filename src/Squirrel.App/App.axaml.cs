using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Squirrel.App.Api;
using Squirrel.App.ViewModels;
using Squirrel.App.Views;
using Squirrel.Core;

namespace Squirrel.App;

public class App : Application
{
    private ApiServer? _apiServer;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new SquirrelStore();
            _apiServer = new ApiServer(store);

            // Start the local capture API in the background; the UI never
            // waits on it, and a port conflict shouldn't kill the app.
            _ = Task.Run(async () =>
            {
                try { await _apiServer.StartAsync(); }
                catch (Exception ex) { Console.Error.WriteLine($"API failed to start: {ex.Message}"); }
            });

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(store, _apiServer.BaseUrl)
            };

            desktop.ShutdownRequested += (_, _) =>
            {
                try { _apiServer.StopAsync().GetAwaiter().GetResult(); }
                catch { /* best effort on shutdown */ }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
