using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;
using Novolis.Pdf.Abstractions;
using Novolis.Pdf.Platform;

namespace NovolisPdfReader;

/// <summary>Receives Android PDF content-URI activations without copying them to shared storage.</summary>
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
    DataMimeType = "application/pdf")]
public sealed class MainActivity : MauiAppCompatActivity
{
    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        PublishPdfIntent(Intent);
    }

    /// <inheritdoc />
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        PublishPdfIntent(intent);
    }

    private void PublishPdfIntent(Intent? intent)
    {
        if (intent?.Action != Intent.ActionView || intent.Data is not { } uri)
            return;

        TryPersistReadPermission(intent, uri);
        var name = ResolveDisplayName(uri);
        PdfActivationBridge.Publish(new PdfOpenRequest(
            new PdfSourceDescriptor(name, uri.ToString()),
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stream = ContentResolver?.OpenInputStream(uri)
                    ?? throw new IOException("Android did not provide a readable PDF stream.");
                return ValueTask.FromResult<Stream>(stream);
            }));
    }

    private void TryPersistReadPermission(Intent intent, Android.Net.Uri uri)
    {
        if ((intent.Flags & ActivityFlags.GrantReadUriPermission) == 0)
            return;
        try
        {
            ContentResolver?.TakePersistableUriPermission(
                uri,
                ActivityFlags.GrantReadUriPermission);
        }
        catch (Java.Lang.SecurityException)
        {
            // Providers may grant only a one-session permission.
        }
    }

    private string ResolveDisplayName(Android.Net.Uri uri)
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
        return string.IsNullOrWhiteSpace(fallback) ? "document.pdf" : fallback;
    }
}
