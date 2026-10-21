using Android.Views;

namespace SketchStudio.Android;

/// <summary>Forwards Android pinch scale changes into the shared sketch viewport.</summary>
internal sealed class SketchScaleGestureListener : Java.Lang.Object, ScaleGestureDetector.IOnScaleGestureListener
{
    private readonly MainActivity _activity;

    public SketchScaleGestureListener(MainActivity activity)
    {
        _activity = activity;
    }

    public bool OnScale(ScaleGestureDetector detector)
    {
        _activity.ApplyPinch(
            detector.ScaleFactor,
            new Avalonia.Point(detector.FocusX, detector.FocusY));
        return true;
    }

    public bool OnScaleBegin(ScaleGestureDetector detector) => true;

    public void OnScaleEnd(ScaleGestureDetector detector)
    {
    }
}
