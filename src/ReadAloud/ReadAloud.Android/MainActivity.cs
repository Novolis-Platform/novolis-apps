using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;
using Microsoft.Identity.Client;
using ReadAloud.Reading;

namespace ReadAloud.Android;

[Activity(
    Label = "Read Aloud",
    Theme = "@style/MainTheme",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher",
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter(
    new[] { Intent.ActionSend },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "text/plain")]
public class MainActivity : AvaloniaMainActivity
{
    public static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Current = this;
        AcceptIncoming(Intent);
        base.OnCreate(savedInstanceState);
    }

    protected override void OnResume()
    {
        Current = this;
        base.OnResume();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent is not null)
            Intent = intent;
        AcceptIncoming(intent);
    }

    public override void OnRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == ReadAloudPlaybackSession.NotificationPermissionRequest &&
            grantResults.Length > 0 &&
            grantResults[0] == Permission.Granted)
        {
            ReadAloudPlaybackSession.Current?.RefreshNotification();
        }
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(Current, this))
            Current = null;
        base.OnDestroy();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(
            requestCode,
            resultCode,
            data);
    }

    static void AcceptIncoming(Intent? intent)
    {
        if (intent is null)
            return;

        if (string.Equals(intent.Action, Intent.ActionSend, StringComparison.Ordinal) &&
            string.Equals(intent.Type, "text/plain", StringComparison.OrdinalIgnoreCase))
        {
            var shared = intent.GetStringExtra(Intent.ExtraText);
            if (!string.IsNullOrWhiteSpace(shared))
                SharedTextInbox.Publish(shared);
        }

        var baseline = intent.GetStringExtra("novolis.baseline");
        if (!string.IsNullOrWhiteSpace(baseline))
            BaselineLaunch.Request(baseline);
    }
}
