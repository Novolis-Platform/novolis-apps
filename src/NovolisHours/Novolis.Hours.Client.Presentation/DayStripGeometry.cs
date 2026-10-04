using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Maps local clocks onto a 06:00–20:00 strip and snaps to five minutes.</summary>
public sealed class DayStripGeometry
{
    /// <summary>Left edge of the visible axis.</summary>
    public static readonly TimeOnly AxisStart = new(6, 0);

    /// <summary>Visible duration of the axis.</summary>
    public static readonly TimeSpan AxisLength = TimeSpan.FromHours(14);

    /// <summary>Pointer snap increment.</summary>
    public static readonly TimeSpan Snap = TimeSpan.FromMinutes(5);

    /// <summary>Handle width as a fraction of the axis.</summary>
    public const double HandleRatio = 0.018;

    /// <summary>Converts a local clock to a 0–1 axis ratio, clamped to the visible window.</summary>
    public static double ToRatio(TimeOnly time)
    {
        var offset = time.ToTimeSpan() - AxisStart.ToTimeSpan();
        var ratio = offset.TotalMinutes / AxisLength.TotalMinutes;
        return Math.Clamp(ratio, 0, 1);
    }

    /// <summary>Converts a 0–1 axis ratio to a snapped local clock.</summary>
    public static TimeOnly FromRatio(double ratio)
    {
        var clamped = Math.Clamp(ratio, 0, 1);
        var minutes = AxisStart.ToTimeSpan().TotalMinutes + (clamped * AxisLength.TotalMinutes);
        return SnapTo(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes)));
    }

    /// <summary>Rounds a clock down/up to the nearest five minutes.</summary>
    public static TimeOnly SnapTo(TimeOnly time)
    {
        var minutes = (int)Math.Round(time.ToTimeSpan().TotalMinutes / Snap.TotalMinutes) * (int)Snap.TotalMinutes;
        minutes = Math.Clamp(minutes, 0, (24 * 60) - 5);
        return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes));
    }

    /// <summary>Returns whether a clock sits outside a configured envelope.</summary>
    public static bool IsOutsideEnvelope(TimeOnly time, LocalTimeRangeDto? envelope) =>
        envelope is not null && (time < envelope.Start || time > envelope.End);

    /// <summary>Hit-tests the actual-work lane.</summary>
    public static DayStripHit HitTest(
        double ratio,
        IReadOnlyList<(TimeOnly Start, TimeOnly End)> actual)
    {
        var time = FromRatio(ratio);
        for (var index = 0; index < actual.Count; index++)
        {
            var interval = actual[index];
            var start = ToRatio(interval.Start);
            var end = ToRatio(interval.End);
            if (ratio >= start && ratio <= start + HandleRatio)
            {
                return new DayStripHit(DayStripHitKind.StartHandle, index, time);
            }

            if (ratio <= end && ratio >= end - HandleRatio)
            {
                return new DayStripHit(DayStripHitKind.EndHandle, index, time);
            }

            if (ratio >= start && ratio <= end)
            {
                return new DayStripHit(DayStripHitKind.Body, index, time);
            }
        }

        return new DayStripHit(DayStripHitKind.Empty, -1, time);
    }
}
