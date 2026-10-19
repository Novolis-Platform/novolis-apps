using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;

namespace NovolisMaps.Android;

/// <summary>Main Avalonia activity for Novolis Maps.</summary>
[Activity(
    Label = "Novolis Maps",
    MainLauncher = true,
    Exported = true,
    Theme = "@style/NovolisMapsTheme",
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    /// <summary>Activity currently hosting the map surface.</summary>
    public static MainActivity? Current { get; private set; }

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Current = this;
        base.OnCreate(savedInstanceState);
    }

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        if (ReferenceEquals(Current, this))
            Current = null;
        base.OnDestroy();
    }
}
