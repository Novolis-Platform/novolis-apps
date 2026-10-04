using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Hours.Client.Presentation;

namespace Novolis.Hours.Client.Avalonia;

/// <summary>Pointer-driven 06:00–20:00 day strip bound to <see cref="DayStudioModel"/>.</summary>
public sealed class HoursDayStripControl : Control
{
    /// <summary>Studio that owns actual intervals and paint.</summary>
    public static readonly StyledProperty<DayStudioModel?> StudioProperty =
        AvaloniaProperty.Register<HoursDayStripControl, DayStudioModel?>(nameof(Studio));

    /// <summary>When true, pointer gestures paint instead of moving actual work.</summary>
    public static readonly StyledProperty<bool> PaintModeProperty =
        AvaloniaProperty.Register<HoursDayStripControl, bool>(nameof(PaintMode));

    /// <summary>Gets or sets the bound studio.</summary>
    public DayStudioModel? Studio
    {
        get => GetValue(StudioProperty);
        set => SetValue(StudioProperty, value);
    }

    /// <summary>Gets or sets paint-mode.</summary>
    public bool PaintMode
    {
        get => GetValue(PaintModeProperty);
        set => SetValue(PaintModeProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Studio is null)
        {
            return;
        }

        Studio.BeginDrag(Ratio(e), PaintMode);
        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Studio is null || !Studio.IsDragging)
        {
            return;
        }

        Studio.MoveDrag(Ratio(e));
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (Studio is null)
        {
            return;
        }

        Studio.MoveDrag(Ratio(e));
        Studio.EndDrag();
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(GraphicalProfile.RaisedBrush, bounds);
        context.DrawRectangle(new Pen(GraphicalProfile.BorderBrush, 1), bounds);
        if (Studio is null)
        {
            return;
        }

        if (Studio.Day.WorkEnvelopeRange is { } envelope)
        {
            DrawLane(context, envelope.Start, envelope.End, 8, GraphicalProfile.BorderBrush);
        }

        foreach (var core in Studio.Day.CoreHourRanges)
        {
            DrawLane(context, core.Start, core.End, 24, GraphicalProfile.ActionSoftBrush);
        }

        foreach (var routine in Studio.Day.RoutineRanges)
        {
            DrawLane(context, routine.Start, routine.End, 40, GraphicalProfile.AccentFillBrush);
        }

        foreach (var actual in Studio.Actual)
        {
            DrawLane(context, actual.Start, actual.End, 58, GraphicalProfile.ActionBrush);
        }

        foreach (var stroke in Studio.Paints)
        {
            DrawLane(context, stroke.Start, stroke.End, 78, GraphicalProfile.AccentBrush);
        }
    }

    private void DrawLane(DrawingContext context, TimeOnly start, TimeOnly end, double top, IBrush brush)
    {
        var left = DayStripGeometry.ToRatio(start) * Bounds.Width;
        var width = Math.Max(2, (DayStripGeometry.ToRatio(end) - DayStripGeometry.ToRatio(start)) * Bounds.Width);
        context.FillRectangle(brush, new Rect(left, top, width, 12));
    }

    private double Ratio(PointerEventArgs e)
    {
        var width = Bounds.Width;
        return width <= 0 ? 0 : Math.Clamp(e.GetPosition(this).X / width, 0, 1);
    }
}
