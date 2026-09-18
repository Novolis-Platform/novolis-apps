using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Merglyph.Core;

namespace Merglyph;

[Register("dev.novolis.merglyph.MainActivity")]
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize
        | ConfigChanges.Orientation
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.KeyboardHidden
        | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataMimeType = "text/markdown")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataMimeType = "text/x-markdown")]
public sealed class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Publish(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        Publish(intent);
    }

    private void Publish(Intent? intent)
    {
        if (intent?.Action != Intent.ActionView || intent.Data is not { } uri)
            return;

        var name = ResolveName(uri);
        if (!SupportedMarkdownDocuments.IsSupported(name))
            return;

        ActivationBridge.Publish(new DocumentOpenRequest(
            new DocumentName(name),
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stream = ContentResolver?.OpenInputStream(uri)
                    ?? throw new IOException("Android did not provide a readable document stream.");
                return ValueTask.FromResult<Stream>(stream);
            }));
    }

    private string ResolveName(Android.Net.Uri uri)
    {
        using ICursor? cursor = ContentResolver?.Query(uri, [IOpenableColumns.DisplayName], null, null, null);
        if (cursor?.MoveToFirst() is true)
        {
            var column = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
            if (column >= 0 && cursor.GetString(column) is { Length: > 0 } displayName)
                return displayName;
        }

        var fallback = Android.Net.Uri.Decode(uri.LastPathSegment ?? string.Empty);
        return string.IsNullOrWhiteSpace(fallback) ? "document.md" : fallback;
    }
}
