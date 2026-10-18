using Avalonia;
using Avalonia.Controls;
using Novolis.Avalonia.GraphicalProfile;

namespace Sudoku.App;

/// <summary>Main Avalonia window for the Sudoku game.</summary>
public sealed class MainWindow : Window
{
    /// <summary>Creates the main window over the shared game service.</summary>
    public MainWindow(SudokuGameService game)
    {
        ArgumentNullException.ThrowIfNull(game);
        Title = "Novolis Sudoku";
        Width = 1_120;
        Height = 820;
        MinWidth = 860;
        MinHeight = 660;
        GraphicalProfile.ApplyWindowChrome(this);
        Content = new SudokuView(game);
    }
}
