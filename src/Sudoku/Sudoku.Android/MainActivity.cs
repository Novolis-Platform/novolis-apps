using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls.ApplicationLifetimes;
using Sudoku.App;

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
    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        BackRequested += OnAndroidBackRequested;
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        BackRequested -= OnAndroidBackRequested;
        base.OnDestroy();
    }

    private void OnAndroidBackRequested(object? sender, AndroidBackRequestedEventArgs e)
    {
        // Prefer TopLevel.BackRequested (SudokuShell); this catches hosts where
        // the activity event is the only signal that fires.
        if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime
            {
                MainView: SudokuShell shell
            }
            && shell.TryHandleBack())
        {
            e.Handled = true;
        }
    }
}
