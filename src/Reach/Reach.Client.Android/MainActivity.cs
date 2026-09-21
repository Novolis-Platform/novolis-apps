using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;

namespace Novolis.Reach.Client.Android;

[Activity(
    Label = "Novolis Reach",
    Theme = "@style/MainTheme",
    MainLauncher = true,
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher",
    WindowSoftInputMode = global::Android.Views.SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
