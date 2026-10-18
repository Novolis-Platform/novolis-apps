using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Merglyph.Core;
using Novolis.Maui.Activation;

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
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "text/markdown")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "text/x-markdown")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "text/plain")]
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

        TryPersistReadPermission(intent, uri);
        var name = ResolveName(uri);
        if (!SupportedMarkdownDocuments.IsSupported(name))
            return;

        MauiActivationBridge<DocumentOpenRequest>.Publish(new DocumentOpenRequest(
            new DocumentName(name),
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase)
                    && uri.Path is { Length: > 0 } path
                    && File.Exists(path))
                {
                    return ValueTask.FromResult<Stream>(File.OpenRead(path));
                }

                var stream = ContentResolver?.OpenInputStream(uri)
                    ?? throw new IOException("Android did not provide a readable document stream.");
                return ValueTask.FromResult<Stream>(stream);
            }));
    }

    private void TryPersistReadPermission(Intent intent, Android.Net.Uri uri)
    {
        var persistableFlags = intent.Flags
            & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        if ((intent.Flags & ActivityFlags.GrantPersistableUriPermission) == 0
            || persistableFlags == 0
            || ContentResolver is null)
        {
            return;
        }

        try
        {
            ContentResolver.TakePersistableUriPermission(uri, persistableFlags);
        }
        catch (Java.Lang.SecurityException)
        {
        }
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
