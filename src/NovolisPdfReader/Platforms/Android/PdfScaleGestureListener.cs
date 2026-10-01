using Android.Views;

namespace NovolisPdfReader;

/// <summary>Forwards Android two-finger / emulator Ctrl+drag scale into the reading surface.</summary>
internal sealed class PdfScaleGestureListener : Java.Lang.Object, ScaleGestureDetector.IOnScaleGestureListener
{
    private readonly MainActivity _activity;

    /// <summary>Creates a listener bound to the host activity.</summary>
    public PdfScaleGestureListener(MainActivity activity)
    {
        _activity = activity;
    }

    /// <inheritdoc />
    public bool OnScale(ScaleGestureDetector detector)
    {
        _activity.ApplyPinchScale(detector.ScaleFactor);
        return true;
    }

    /// <inheritdoc />
    public bool OnScaleBegin(ScaleGestureDetector detector)
    {
        _activity.BeginPinchScale();
        return true;
    }

    /// <inheritdoc />
    public void OnScaleEnd(ScaleGestureDetector detector) => _activity.FinishPinchScale();
}
