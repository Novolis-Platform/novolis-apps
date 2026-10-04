using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;
using Android.Views;
using AndroidX.Core.View;
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
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "application/pdf")]
// Some download providers expose a PDF as a generic or legacy application MIME type.
// Keep those providers in the Android resolver without advertising the reader for every
// arbitrary URI.
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "application/x-pdf")]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataSchemes = new[] { "content", "file" },
    DataMimeType = "application/octet-stream")]
public sealed class MainActivity : MauiAppCompatActivity
{
    private ScaleGestureDetector? _scale;
    private bool _scaling;

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (Window is not null)
            WindowCompat.SetDecorFitsSystemWindows(Window, true);
        _scale = new ScaleGestureDetector(this, new PdfScaleGestureListener(this))
        {
            QuickScaleEnabled = false,
        };
        PublishPdfIntent(Intent);
    }

    /// <inheritdoc />
    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e is null || _scale is null)
            return base.DispatchTouchEvent(e);

        _scale.OnTouchEvent(e);
        if (e.PointerCount >= 2)
            Android.Util.Log.Info("NovolisPdf", $"pinch pointers={e.PointerCount} action={e.ActionMasked} scaling={_scaling}");
        if (_scaling || e.PointerCount >= 2)
            return true;

        return base.DispatchTouchEvent(e);
    }

    /// <summary>Marks a host pinch so MAUI one-finger pan does not steal the second pointer.</summary>
    internal void BeginPinchScale()
    {
        _scaling = true;
        Android.Util.Log.Info("NovolisPdf", "pinch begin");
    }

    /// <summary>Applies an incremental scale factor from <see cref="ScaleGestureDetector"/>.</summary>
    internal void ApplyPinchScale(float factor)
    {
        _scaling = true;
        Android.Util.Log.Info("NovolisPdf", $"pinch scale={factor}");
        TryGetMainPage()?.ScaleReading(factor);
    }

    /// <summary>Ends a host pinch and re-rasterizes.</summary>
    internal void FinishPinchScale()
    {
        if (!_scaling)
            return;
        _scaling = false;
        TryGetMainPage()?.EndReadingScale();
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
            Keycode.Plus or Keycode.NumpadAdd or Keycode.ZoomIn => "Add",
            Keycode.Minus or Keycode.NumpadSubtract or Keycode.ZoomOut => "Subtract",
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
        var persistableFlags = intent.Flags
            & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        if ((intent.Flags & ActivityFlags.GrantPersistableUriPermission) == 0
            || persistableFlags == 0
            || ContentResolver is null)
            return;
        try
        {
            ContentResolver.TakePersistableUriPermission(uri, persistableFlags);
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
