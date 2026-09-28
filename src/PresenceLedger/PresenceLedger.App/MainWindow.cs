using Avalonia.Controls;

namespace PresenceLedger.App;

/// <summary>Local desktop shell for the shared Presence Ledger view.</summary>
public sealed class MainWindow : Window
{
    /// <summary>Creates the application window.</summary>
    public MainWindow()
    {
        Title = "Presence Ledger";
        Width = 1100;
        Height = 760;
        MinWidth = 420;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}
