using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Identity.Client;

namespace ReadAloud.Android;

/// <summary>Receives the embedded WebView redirect and returns it to MSAL.</summary>
[Activity(
    Exported = true,
    LaunchMode = LaunchMode.SingleTask,
    NoHistory = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault },
    DataHost = "auth",
    DataScheme = "msalc8b938aa-2e5d-48b4-89c6-fc139733c44d")]
public sealed class MsalActivity : BrowserTabActivity
{
}
