using Avalonia;
using Avalonia.Input;
using Novolis.Reach.Client;

namespace Novolis.Avalonia.Reach;

internal static class ReachClientViewTransform
{
    internal static void Apply(ReachClientView view, double zoom, double panX, double panY)
    {
        var bounds = view._videoSurface.Bounds;
        var fit = ReachVideoGeometry.CalculateFit(
            bounds.Width,
            bounds.Height,
            view._videoWidth,
            view._videoHeight,
            zoom,
            panX,
            panY);
        if (fit.Scale <= 0)
            return;

        view._videoZoom = fit.Zoom;
        view._videoScale.ScaleX = fit.Zoom;
        view._videoScale.ScaleY = fit.Zoom;
        view._videoTranslation.X = fit.PanX;
        view._videoTranslation.Y = fit.PanY;
    }

    internal static void ResetPanIfUnzoomed(ReachClientView view)
    {
        if (view._videoZoom <= 1)
            Apply(view, 1, 0, 0);
    }

    internal static double Distance(Point first, Point second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return Math.Sqrt(x * x + y * y);
    }

    internal static Point Midpoint(Point first, Point second) =>
        new((first.X + second.X) / 2, (first.Y + second.Y) / 2);

    internal static void OnWheel(
        ReachClientView view,
        object? sender,
        PointerWheelEventArgs args)
    {
        var delta = (int)Math.Round(args.Delta.Y * 120);
        if (delta == 0)
            return;

        view.QueueInput(() => view._session.SendPointerWheelAsync(delta));
        args.Handled = true;
    }
}
