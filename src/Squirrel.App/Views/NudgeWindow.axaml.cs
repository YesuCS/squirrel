using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Squirrel.App.Views;

/// <summary>
/// A gentle corner nudge. Deliberately not a system notification: it never
/// steals focus (ShowActivated=false), it auto-dismisses, and its copy stays
/// guilt-free. "Later" is always an acceptable answer.
/// </summary>
public partial class NudgeWindow : Window
{
    private readonly Action? _onShow;

    // Parameterless ctor for the XAML previewer only.
    public NudgeWindow() => InitializeComponent();

    public NudgeWindow(string message, Action onShow) : this()
    {
        _onShow = onShow;
        MessageText.Text = message;

        ShowButton.Click += (_, _) => { _onShow?.Invoke(); Close(); };
        LaterButton.Click += (_, _) => Close();

        DispatcherTimer.RunOnce(() => Close(), TimeSpan.FromSeconds(15));
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Bottom-right corner of the primary screen's working area.
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null) return;

        var workingArea = screen.WorkingArea;
        var width = (int)(Bounds.Width * RenderScaling);
        var height = (int)(Bounds.Height * RenderScaling);
        Position = new PixelPoint(
            workingArea.Right - width - 24,
            workingArea.Bottom - height - 24);
    }
}
