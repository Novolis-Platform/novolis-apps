using System.Collections.Immutable;
using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Editable day strip: actual intervals, paint, and live layer rail.</summary>
public sealed class DayStudioModel
{
    private readonly List<(TimeOnly Start, TimeOnly End)> actual = [];
    private readonly List<PaintStroke> paints = [];
    private DayStripDragKind dragKind;
    private int dragIndex = -1;
    private TimeOnly dragOrigin;
    private TimeOnly dragStartSnapshot;
    private TimeOnly dragEndSnapshot;

    /// <summary>Creates a studio from a projected WorkDay.</summary>
    public DayStudioModel(WorkDayResponse day)
    {
        ArgumentNullException.ThrowIfNull(day);
        Day = day;
        foreach (var interval in day.WorkedIntervals)
        {
            actual.Add((TimeOnly.FromTimeSpan(interval.Start.TimeOfDay), TimeOnly.FromTimeSpan(interval.End.TimeOfDay)));
        }

        foreach (var measure in day.Dimensions)
        {
            if (measure.Interval is null)
            {
                continue;
            }

            paints.Add(new PaintStroke(
                measure.DimensionId,
                measure.ValueId,
                TimeOnly.FromTimeSpan(measure.Interval.Start.TimeOfDay),
                TimeOnly.FromTimeSpan(measure.Interval.End.TimeOfDay)));
        }
    }

    /// <summary>Source projection.</summary>
    public WorkDayResponse Day { get; }

    /// <summary>Actual intervals currently on the strip.</summary>
    public IReadOnlyList<(TimeOnly Start, TimeOnly End)> Actual => actual;

    /// <summary>Paint strokes clipped to actual work.</summary>
    public IReadOnlyList<PaintStroke> Paints => paints;

    /// <summary>Active brush Dimension.</summary>
    public string BrushDimensionId { get; set; } = "customer";

    /// <summary>Active brush value.</summary>
    public string BrushValueId { get; set; } = "acme";

    /// <summary>Whether a pointer gesture is in progress.</summary>
    public bool IsDragging => dragKind != DayStripDragKind.None;

    /// <summary>Builds the layer rail. Silence is an empty <see cref="LayerRailRow.Said"/>.</summary>
    public ImmutableArray<LayerRailRow> LayerRail()
    {
        var groups = Day.AppliedRules
            .GroupBy(rule => rule.LayerKind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        string[] kinds = ["National", "Organisation", "Agreement", "Employment", "Employee", "TemporaryOverride"];
        int[] orders = [100, 200, 300, 400, 500, 600];
        var rows = ImmutableArray.CreateBuilder<LayerRailRow>(kinds.Length);
        for (var index = 0; index < kinds.Length; index++)
        {
            var kind = kinds[index];
            if (!groups.TryGetValue(kind, out var rules) || rules.Length == 0)
            {
                rows.Add(new LayerRailRow(kind, orders[index], string.Empty, false));
                continue;
            }

            var said = string.Join(" · ", rules.Select(rule => rule.Summary));
            var hit = rules.Any(rule =>
                rule.RuleType.Contains("WorkingDay", StringComparison.Ordinal) ||
                rule.RuleType.Contains("WorkEnvelope", StringComparison.Ordinal) ||
                rule.HolidayId is not null ||
                rule.RuleType.Contains("PaidEntitlement", StringComparison.Ordinal) ||
                rule.RuleType.Contains("ExpectedWork", StringComparison.Ordinal));
            if (HasOutsideEnvelope() &&
                string.Equals(kind, "Organisation", StringComparison.Ordinal))
            {
                said += " · outside envelope";
                hit = true;
            }

            rows.Add(new LayerRailRow(kind, orders[index], said, hit));
        }

        return rows.ToImmutable();
    }

    /// <summary>True when any actual clock sits outside the organisation envelope.</summary>
    public bool HasOutsideEnvelope()
    {
        var envelope = Day.WorkEnvelopeRange;
        return actual.Any(interval =>
            DayStripGeometry.IsOutsideEnvelope(interval.Start, envelope) ||
            DayStripGeometry.IsOutsideEnvelope(interval.End, envelope));
    }

    /// <summary>Expected work formatted for chrome.</summary>
    public string ExpectedLabel => HoursClock.Format(Day.ExpectedWork);

    /// <summary>Actual work from the current strip, not the last saved projection.</summary>
    public TimeSpan ActualWork
    {
        get
        {
            var total = TimeSpan.Zero;
            foreach (var interval in actual)
            {
                total += interval.End - interval.Start;
            }

            return total;
        }
    }

    /// <summary>Flex for the in-progress strip.</summary>
    public TimeSpan Flex => ActualWork - Day.ExpectedWork;

    /// <summary>Whether this workplace treats surplus as flex.</summary>
    public bool AllowsFlex => Day.AllowsFlex;

    /// <summary>Whether the employee writes attendance instead of a flex timesheet.</summary>
    public bool AttendanceConfirmationOnly => Day.AttendanceConfirmationOnly;

    /// <summary>Whether paint and drag belong on this strip.</summary>
    public bool AllowsPaint => Day.AllowsFlex;

    /// <summary>Primary commit label for this workplace.</summary>
    public string CommitLabel =>
        Day.AttendanceConfirmationOnly ? "I was here" : "Worked as scheduled";

    /// <summary>Registration note written with the primary commit.</summary>
    public string CommitNote =>
        Day.AttendanceConfirmationOnly
            ? "I confirm I attended the contracted shop hours."
            : "Worked as scheduled.";

    /// <summary>Workplace name shown in chrome.</summary>
    public string WorkplaceName =>
        string.IsNullOrWhiteSpace(Day.OrganisationName) ? Day.OrganisationId : Day.OrganisationName;

    /// <summary>Chrome balance line: flex saldo or shop-hours attendance.</summary>
    public string BalanceLabel =>
        Day.AllowsFlex ? HoursClock.Format(Flex, signed: true) : "Shop hours";

    /// <summary>Starts a drag on the actual lane, or a paint stroke when <paramref name="paint"/> is true.</summary>
    public void BeginDrag(double ratio, bool paint)
    {
        if (paint && !AllowsPaint || !paint && !Day.AllowsFlex)
        {
            dragKind = DayStripDragKind.None;
            return;
        }

        var hit = DayStripGeometry.HitTest(ratio, actual);
        dragOrigin = hit.Time;
        if (paint)
        {
            if (hit.Kind == DayStripHitKind.Empty)
            {
                dragKind = DayStripDragKind.None;
                return;
            }

            dragKind = DayStripDragKind.Paint;
            dragIndex = hit.IntervalIndex;
            return;
        }

        dragKind = hit.Kind switch
        {
            DayStripHitKind.StartHandle => DayStripDragKind.ResizeStart,
            DayStripHitKind.EndHandle => DayStripDragKind.ResizeEnd,
            DayStripHitKind.Body => DayStripDragKind.Move,
            _ => DayStripDragKind.Create,
        };
        dragIndex = hit.IntervalIndex;
        if (dragKind == DayStripDragKind.Create)
        {
            actual.Add((hit.Time, hit.Time.Add(DayStripGeometry.Snap)));
            dragIndex = actual.Count - 1;
        }

        if (dragIndex >= 0)
        {
            dragStartSnapshot = actual[dragIndex].Start;
            dragEndSnapshot = actual[dragIndex].End;
        }
    }

    /// <summary>Updates the active gesture.</summary>
    public void MoveDrag(double ratio)
    {
        if (dragKind == DayStripDragKind.None)
        {
            return;
        }

        var time = DayStripGeometry.FromRatio(ratio);
        if (dragKind == DayStripDragKind.Paint)
        {
            ApplyPaint(dragOrigin, time);
            return;
        }

        if (dragIndex < 0 || dragIndex >= actual.Count)
        {
            return;
        }

        var current = actual[dragIndex];
        actual[dragIndex] = dragKind switch
        {
            DayStripDragKind.ResizeStart => (Min(time, current.End.Add(-DayStripGeometry.Snap)), current.End),
            DayStripDragKind.ResizeEnd => (current.Start, Max(time, current.Start.Add(DayStripGeometry.Snap))),
            DayStripDragKind.Move or DayStripDragKind.Create => Move(dragStartSnapshot, dragEndSnapshot, time - dragOrigin),
            _ => current,
        };
    }

    /// <summary>Ends the active gesture and drops empty create intervals.</summary>
    public void EndDrag()
    {
        if (dragKind == DayStripDragKind.Create &&
            dragIndex >= 0 &&
            dragIndex < actual.Count &&
            actual[dragIndex].End <= actual[dragIndex].Start.Add(DayStripGeometry.Snap))
        {
            // keep the five-minute interval created on pointer down
        }

        dragKind = DayStripDragKind.None;
        dragIndex = -1;
    }

    /// <summary>Replaces actual work with the routine blocks (worked-as-scheduled preview).</summary>
    public void ApplyScheduledRoutine()
    {
        actual.Clear();
        foreach (var range in Day.RoutineRanges)
        {
            actual.Add((range.Start, range.End));
        }

        paints.Clear();
    }

    /// <summary>Clears unsaved paint.</summary>
    public void ClearPaint() => paints.Clear();

    /// <summary>Clips a candidate stroke to overlapping actual work. Returns false when nothing remains.</summary>
    public bool TryClipPaintToActual(TimeOnly start, TimeOnly end, out TimeOnly clippedStart, out TimeOnly clippedEnd)
    {
        if (end < start)
        {
            (start, end) = (end, start);
        }

        clippedStart = default;
        clippedEnd = default;
        foreach (var interval in actual)
        {
            var overlapStart = start > interval.Start ? start : interval.Start;
            var overlapEnd = end < interval.End ? end : interval.End;
            if (overlapEnd > overlapStart)
            {
                clippedStart = overlapStart;
                clippedEnd = overlapEnd;
                return true;
            }
        }

        return false;
    }

    private void ApplyPaint(TimeOnly origin, TimeOnly current)
    {
        if (!TryClipPaintToActual(origin, current, out var start, out var end))
        {
            return;
        }

        paints.RemoveAll(stroke =>
            stroke.DimensionId == BrushDimensionId &&
            stroke.Start < end &&
            stroke.End > start);
        paints.Add(new PaintStroke(BrushDimensionId, BrushValueId, start, end));
    }

    private static (TimeOnly Start, TimeOnly End) Move(TimeOnly start, TimeOnly end, TimeSpan delta)
    {
        var nextStart = DayStripGeometry.SnapTo(start.Add(delta));
        var length = end - start;
        var nextEnd = nextStart.Add(length);
        if (nextEnd <= nextStart)
        {
            nextEnd = nextStart.Add(DayStripGeometry.Snap);
        }

        return (nextStart, nextEnd);
    }

    private static TimeOnly Min(TimeOnly left, TimeOnly right) => left <= right ? left : right;

    private static TimeOnly Max(TimeOnly left, TimeOnly right) => left >= right ? left : right;
}
