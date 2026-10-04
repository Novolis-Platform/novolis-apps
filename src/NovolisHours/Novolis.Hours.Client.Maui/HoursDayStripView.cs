using Microsoft.Maui.Graphics;
using Novolis.Hours.Client.Presentation;
using Novolis.Maui.GraphicalProfile;

namespace Novolis.Hours.Client.Maui;

/// <summary>Horizontally scrollable 06:00–20:00 strip with pan-driven drag and paint.</summary>
public sealed class HoursDayStripView : GraphicsView
{
    /// <summary>Creates a profile-sized strip that maps pan X onto <see cref="DayStudioModel"/>.</summary>
    public HoursDayStripView()
    {
        Drawable = new StripDrawable(this);
        HeightRequest = 108;
        MinimumHeightRequest = 42;
        StartInteraction += OnStart;
        DragInteraction += OnDrag;
        EndInteraction += OnEnd;
        CancelInteraction += (_, _) => EndDrag();
    }

    /// <summary>Bound studio.</summary>
    public DayStudioModel? Studio { get; set; }

    /// <summary>Paint versus adjust.</summary>
    public bool PaintMode { get; set; }

    /// <summary>Raised after a gesture updates the studio.</summary>
    public event EventHandler? StudioChanged;

    private void OnStart(object? sender, TouchEventArgs args)
    {
        if (Studio is null || args.Touches.Length == 0)
        {
            return;
        }

        Studio.BeginDrag(Ratio(args.Touches[0].X), PaintMode);
        Invalidate();
        StudioChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDrag(object? sender, TouchEventArgs args)
    {
        if (Studio is null || !Studio.IsDragging || args.Touches.Length == 0)
        {
            return;
        }

        Studio.MoveDrag(Ratio(args.Touches[0].X));
        Invalidate();
        StudioChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnEnd(object? sender, TouchEventArgs args)
    {
        if (Studio is null || args.Touches.Length == 0)
        {
            EndDrag();
            return;
        }

        Studio.MoveDrag(Ratio(args.Touches[0].X));
        EndDrag();
    }

    private void EndDrag()
    {
        if (Studio is null)
        {
            return;
        }

        Studio.EndDrag();
        Invalidate();
        StudioChanged?.Invoke(this, EventArgs.Empty);
    }

    private double Ratio(float x)
    {
        var width = Width > 0 ? Width : 960;
        return Math.Clamp(x / width, 0, 1);
    }

    private sealed class StripDrawable(HoursDayStripView owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = GraphicalProfile.Raised;
            canvas.FillRectangle(dirtyRect);
            canvas.StrokeColor = GraphicalProfile.Border;
            canvas.StrokeSize = 1;
            canvas.DrawRectangle(dirtyRect);
            if (owner.Studio is null)
            {
                return;
            }

            if (owner.Studio.Day.WorkEnvelopeRange is { } envelope)
            {
                DrawLane(canvas, dirtyRect, envelope.Start, envelope.End, 8, GraphicalProfile.Border);
            }

            foreach (var core in owner.Studio.Day.CoreHourRanges)
            {
                DrawLane(canvas, dirtyRect, core.Start, core.End, 24, GraphicalProfile.ActionSoft);
            }

            foreach (var routine in owner.Studio.Day.RoutineRanges)
            {
                DrawLane(canvas, dirtyRect, routine.Start, routine.End, 40, GraphicalProfile.AccentFill);
            }

            foreach (var actual in owner.Studio.Actual)
            {
                DrawLane(canvas, dirtyRect, actual.Start, actual.End, 58, GraphicalProfile.Action);
            }

            foreach (var stroke in owner.Studio.Paints)
            {
                DrawLane(canvas, dirtyRect, stroke.Start, stroke.End, 78, GraphicalProfile.Accent);
            }
        }

        private static void DrawLane(
            ICanvas canvas,
            RectF dirty,
            TimeOnly start,
            TimeOnly end,
            float top,
            Color color)
        {
            var left = (float)(DayStripGeometry.ToRatio(start) * dirty.Width);
            var width = Math.Max(8, (float)((DayStripGeometry.ToRatio(end) - DayStripGeometry.ToRatio(start)) * dirty.Width));
            canvas.FillColor = color;
            canvas.FillRectangle(left, top, width, 12);
        }
    }
}
