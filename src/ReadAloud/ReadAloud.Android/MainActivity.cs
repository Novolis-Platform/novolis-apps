using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;
using Microsoft.Identity.Client;

namespace ReadAloud.Android;

[Activity(
    Label = "Read Aloud",
    Theme = "@style/MainTheme",
    MainLauncher = true,
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher",
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    public static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Current = this;
        base.OnCreate(savedInstanceState);
    }

    protected override void OnResume()
    {
        Current = this;
        base.OnResume();
    }

    protected override void OnPause()
    {
        if (ReferenceEquals(Current, this))
            Current = null;
        base.OnPause();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(
            requestCode,
            resultCode,
            data!);
        base.OnActivityResult(requestCode, resultCode, data);
    }
}
