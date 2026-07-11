using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Squirrel.Core;

namespace Squirrel.App.Views;

public partial class QuickCaptureWindow : Window
{
    private readonly SquirrelStore? _store;

    // Parameterless ctor for the XAML previewer only.
    public QuickCaptureWindow() => InitializeComponent();

    public QuickCaptureWindow(SquirrelStore store) : this() => _store = store;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        CaptureBox.Focus();
    }

    private void OnCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var text = CaptureBox.Text;
            if (!string.IsNullOrWhiteSpace(text))
                _store?.Capture(text.Trim(), "quick");
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
