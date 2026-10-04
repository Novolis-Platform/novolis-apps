using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Novolis.Maui.Activation;

namespace Novolis.Ndjson.App;

[Register("dev.novolis.ndjsonviewer.MainActivity")]
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
    DataMimeType = "application/x-ndjson")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "application/ndjson")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "text/x-ndjson")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "application/json")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "application/octet-stream")]
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
        var hasNdjsonExtension = Path.GetExtension(name)
            .Equals(".ndjson", StringComparison.OrdinalIgnoreCase);
        if (!hasNdjsonExtension && !IsNdjsonMimeType(intent.Type))
            return;

        if (!hasNdjsonExtension && IsNdjsonMimeType(intent.Type)
            && Path.GetFileName(name).IndexOf('.') < 0)
        {
            name += ".ndjson";
        }

        MauiActivationBridge<NdjsonOpenRequest>.Publish(NdjsonOpenRequest.FromStream(
            name,
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
                    ?? throw new IOException("Android did not provide a readable NDJSON stream.");
                return ValueTask.FromResult<Stream>(stream);
            }));
    }

    private static bool IsNdjsonMimeType(string? mimeType) =>
        string.Equals(mimeType, "application/x-ndjson", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mimeType, "application/ndjson", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mimeType, "text/x-ndjson", StringComparison.OrdinalIgnoreCase);

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
            // Providers may grant only a one-session permission.
        }
    }

    private string ResolveName(Android.Net.Uri uri)
    {
        using ICursor? cursor = ContentResolver?.Query(
            uri,
            [IOpenableColumns.DisplayName],
            null,
            null,
            null);
        if (cursor?.MoveToFirst() is true)
        {
            var column = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
            if (column >= 0 && cursor.GetString(column) is { Length: > 0 } displayName)
                return displayName;
        }

        var fallback = Android.Net.Uri.Decode(uri.LastPathSegment ?? string.Empty);
        return string.IsNullOrWhiteSpace(fallback) ? "document.ndjson" : fallback;
    }
}
