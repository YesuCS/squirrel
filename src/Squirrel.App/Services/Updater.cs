using Velopack;
using Velopack.Sources;

namespace Squirrel.App.Services;

/// <summary>
/// Velopack auto-update against GitHub Releases. Checks quietly in the
/// background on startup; if an update was downloaded, it is applied when the
/// app exits so it never interrupts you mid-thought.
/// Does nothing when running unpackaged (dotnet run / plain publish folder).
/// </summary>
public static class Updater
{
    public const string RepoUrl = "https://github.com/YesuCS/squirrel";

    private static UpdateManager? _manager;
    private static UpdateInfo? _pending;

    public static bool UpdateReady => _pending is not null;

    public static async Task CheckAndDownloadAsync()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
            if (!manager.IsInstalled)
                return; // running from source or a bare publish folder

            var info = await manager.CheckForUpdatesAsync();
            if (info is null)
                return;

            await manager.DownloadUpdatesAsync(info);
            _manager = manager;
            _pending = info;
        }
        catch
        {
            // Never let update plumbing break the app; try again next launch.
        }
    }

    /// <summary>Call during shutdown; swaps in the new version after exit.</summary>
    public static void ApplyOnExitIfReady()
    {
        try
        {
            if (_manager is not null && _pending is not null)
                _manager.WaitExitThenApplyUpdates(_pending);
        }
        catch
        {
            // Best effort; worst case the update applies on a later run.
        }
    }
}
