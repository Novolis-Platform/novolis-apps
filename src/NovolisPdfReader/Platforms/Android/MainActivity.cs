using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;
using Android.Views;
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
    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        var control = e is not null && (e.MetaState & MetaKeyStates.CtrlOn) != 0;
        var key = keyCode switch
        {
            Keycode.PageUp => "PageUp",
            Keycode.PageDown => "PageDown",
            Keycode.DpadLeft => "Left",
            Keycode.DpadRight => "Right",
            Keycode.MoveHome or Keycode.Home => "Home",
            Keycode.MoveEnd => "End",
            Keycode.Plus or Keycode.NumpadAdd => "Add",
            Keycode.Minus or Keycode.NumpadSubtract => "Subtract",
            Keycode.F => "F",
            Keycode.R => "R",
            Keycode.G => "G",
            _ => null,
        };
        if (key is not null
            && TryGetMainPage() is { } page
            && page.TryHandleKey(key, control))
        {
            return true;
        }

        return base.OnKeyDown(keyCode, e);
    }

    /// <inheritdoc />
    public override bool DispatchGenericMotionEvent(MotionEvent? e)
    {
        if (e is { Action: MotionEventActions.Scroll }
            && (e.Source & InputSourceType.ClassPointer) != 0)
        {
            var control = (e.MetaState & MetaKeyStates.CtrlOn) != 0;
            var delta = e.GetAxisValue(Axis.Vscroll);
            if (TryGetMainPage() is { } page && page.TryHandleWheel(delta, control))
                return true;
        }

        return base.DispatchGenericMotionEvent(e);
    }

    private static MainPage? TryGetMainPage() =>
        Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page as MainPage;

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
                return ValueTask.FromResult(OpenPdfStream(uri));
            }));
    }

    private Stream OpenPdfStream(Android.Net.Uri uri)
    {
        if (string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase)
            && uri.Path is { Length: > 0 } path)
        {
            try
            {
                if (File.Exists(path))
                    return File.OpenRead(path);
            }
            catch (IOException)
            {
            }
        }

        return ContentResolver?.OpenInputStream(uri)
            ?? throw new IOException("Android did not provide a readable PDF stream.");
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
