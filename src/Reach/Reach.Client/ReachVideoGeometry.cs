using Novolis.Video;

namespace Novolis.Reach.Client;

/// <summary>Calculates fit, zoom, pan, and source-display coordinates.</summary>
public static class ReachVideoGeometry
{
    /// <summary>Calculates a centered fit for a remote video frame.</summary>
    public static ReachVideoFit CalculateFit(
        double surfaceWidth,
        double surfaceHeight,
        int videoWidth,
        int videoHeight,
        double zoom,
        double panX,
        double panY)
    {
        var fit = VideoGeometry.CalculateFit(
            surfaceWidth,
            surfaceHeight,
            videoWidth,
            videoHeight,
            zoom,
            panX,
            panY);
        return new ReachVideoFit(
            fit.Scale,
            fit.Zoom,
            fit.OriginX,
            fit.OriginY,
            fit.RenderedWidth,
            fit.RenderedHeight,
            fit.PanX,
            fit.PanY);
    }

    /// <summary>
    /// Maps a point in the fitted surface to the selected source display.
    /// </summary>
    public static bool TryMapPoint(
        ReachVideoFit fit,
        double pointX,
        double pointY,
        int videoWidth,
        int videoHeight,
        int displayLeft,
        int displayTop,
        int displayWidth,
        int displayHeight,
        out double sourceX,
        out double sourceY) =>
        VideoGeometry.TryMapPoint(
            new VideoFit(
                fit.Scale,
                fit.Zoom,
                fit.OriginX,
                fit.OriginY,
                fit.RenderedWidth,
                fit.RenderedHeight,
                fit.PanX,
                fit.PanY),
            pointX,
            pointY,
            videoWidth,
            videoHeight,
            displayLeft,
            displayTop,
            displayWidth,
            displayHeight,
            out sourceX,
            out sourceY);
}
