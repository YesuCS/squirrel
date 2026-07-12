using Avalonia.Controls;

namespace Squirrel.App.Views;

public partial class ManualWindow : Window
{
    // Parameterless ctor for the XAML previewer only.
    public ManualWindow() => InitializeComponent();

    public ManualWindow(string manualText) : this() => ManualBlock.Text = manualText;
}
