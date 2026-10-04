using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Month review numbers derived from already-explained days.</summary>
public sealed class ReviewMonthModel
{
    /// <summary>Creates a month summary from a review projection and the days in range.</summary>
    public ReviewMonthModel(ReviewProjectionResponse review, IReadOnlyList<WorkDayResponse> days)
    {
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(days);
        Review = review;
        Days = days;
        ExpectedWork = days.Aggregate(TimeSpan.Zero, (total, day) => total + day.ExpectedWork);
        ActualWork = days.Aggregate(TimeSpan.Zero, (total, day) => total + day.ActualWorked);
    }

    /// <summary>Server review projection.</summary>
    public ReviewProjectionResponse Review { get; }

    /// <summary>WorkDays covering the period.</summary>
    public IReadOnlyList<WorkDayResponse> Days { get; }

    /// <summary>Sum of expected work.</summary>
    public TimeSpan ExpectedWork { get; }

    /// <summary>Sum of recorded actual work.</summary>
    public TimeSpan ActualWork { get; }
}
