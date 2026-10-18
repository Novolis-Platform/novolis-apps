using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;

namespace Sudoku.Android;

/// <summary>Main Avalonia activity for the Android Sudoku product.</summary>
[Activity(
    Label = "Novolis Sudoku",
    MainLauncher = true,
    Exported = true,
    Theme = "@style/SudokuTheme",
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
