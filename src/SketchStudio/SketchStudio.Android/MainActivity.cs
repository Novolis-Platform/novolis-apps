using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Android;
using Avalonia.Controls.ApplicationLifetimes;

namespace SketchStudio.Android;

/// <summary>Main Avalonia activity for Sketch Studio on Android.</summary>
[Activity(
    Label = "Sketch Studio",
    Theme = "@style/SketchStudioTheme",
    MainLauncher = true,
    Exported = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    private ScaleGestureDetector? _scale;
    private bool _multiTouch;
    private Avalonia.Point? _lastMultiTouchFocus;

    /// <inheritdoc />
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _scale = new ScaleGestureDetector(this, new SketchScaleGestureListener(this))
        {
            QuickScaleEnabled = false,
        };
    }

    /// <summary>Routes one-finger drawing to Avalonia and reserves two fingers for the viewport.</summary>
    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e is null || _scale is null)
            return base.DispatchTouchEvent(e);

        if (e.PointerCount >= 2)
        {
            if (!_multiTouch)
            {
                _multiTouch = true;
                _lastMultiTouchFocus = null;
                TryGetSketchView()?.BeginMultiTouch();
            }

            _scale.OnTouchEvent(e);
            var focus = MultiTouchFocus(e);
            if (_lastMultiTouchFocus is { } last)
            {
                TryGetSketchView()?.PanMultiTouch(
                    new Avalonia.Vector(focus.X - last.X, focus.Y - last.Y));
            }

            _lastMultiTouchFocus = focus;
            return true;
        }

        if (_multiTouch)
        {
            _scale.OnTouchEvent(e);
            _multiTouch = false;
            _lastMultiTouchFocus = null;
            return true;
        }

        _scale.OnTouchEvent(e);
        return base.DispatchTouchEvent(e);
    }

    internal void ApplyPinch(double factor, Avalonia.Point focus) =>
        TryGetSketchView()?.ApplyMultiTouchZoom(factor, focus);

    private static Avalonia.Point MultiTouchFocus(MotionEvent e)
    {
        var x = 0f;
        var y = 0f;
        for (var index = 0; index < e.PointerCount; index++)
        {
            x += e.GetX(index);
            y += e.GetY(index);
        }

        return new Avalonia.Point(
            x / Math.Max(1, e.PointerCount),
            y / Math.Max(1, e.PointerCount));
    }

    private static SketchMobileView? TryGetSketchView() =>
        (Avalonia.Application.Current?.ApplicationLifetime as ISingleViewApplicationLifetime)
            ?.MainView as SketchMobileView;
}
