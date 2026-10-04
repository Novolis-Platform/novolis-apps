using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>Non-authoritative comparison between routine and resolved work.</summary>
public sealed record RoutineComparison
{
    /// <summary>Initializes a routine comparison.</summary>
    public RoutineComparison(
        TimeSpan outsideRoutine,
        TimeSpan missingRoutine,
        IEnumerable<RoutineDifference> differences)
    {
        if (outsideRoutine < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(outsideRoutine));
        }

        if (missingRoutine < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(missingRoutine));
        }

        ArgumentNullException.ThrowIfNull(differences);
        OutsideRoutine = outsideRoutine;
        MissingRoutine = missingRoutine;
        Differences = differences.ToImmutableArray();
    }

    /// <summary>Observed duration outside configured routine.</summary>
    public TimeSpan OutsideRoutine { get; }

    /// <summary>Routine duration without corresponding observed work.</summary>
    public TimeSpan MissingRoutine { get; }

    /// <summary>Ordered, neutral comparison descriptions.</summary>
    public ImmutableArray<RoutineDifference> Differences { get; }
}
