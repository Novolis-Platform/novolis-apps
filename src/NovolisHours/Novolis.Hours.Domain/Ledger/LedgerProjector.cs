using Novolis.Hours.Domain.Dimensions;

namespace Novolis.Hours.Domain.Ledger;

/// <summary>Projects explicitly mapped derived measures into balanced duration transactions.</summary>
public sealed class LedgerProjector
{
    private readonly IReadOnlyList<DimensionLedgerMapping> mappings;

    /// <summary>Initializes a projector with explicit derived-value mappings.</summary>
    public LedgerProjector(IEnumerable<DimensionLedgerMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        this.mappings = mappings.ToArray();
    }

    /// <summary>Projects the current Dimension result without mutating any source fact.</summary>
    public LedgerTransaction Project(
        DimensionEvaluation evaluation,
        string reason = "Derived Dimension ledger projection")
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        var movement = GetMappedMovement(evaluation);
        return CreateBalanced(
            evaluation.WorkDay.Key,
            evaluation.WorkDay.EffectiveRegistration.Id,
            movement,
            reason);
    }

    /// <summary>Appends a difference transaction for a corrected projection.</summary>
    public LedgerTransaction CreateCorrection(
        LedgerTransaction original,
        LedgerTransaction corrected,
        string reason = "Corrected Dimension ledger projection")
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(corrected);
        if (original.WorkDay != corrected.WorkDay)
        {
            throw new ArgumentException(
                "Ledger correction transactions must describe the same WorkDay.",
                nameof(corrected));
        }

        var oldMovement = GetAccountMovement(original, DurationAccount.EmployeeFlex);
        var newMovement = GetAccountMovement(corrected, DurationAccount.EmployeeFlex);
        return CreateBalanced(
            corrected.WorkDay,
            corrected.SourceRegistrationId,
            newMovement - oldMovement,
            reason);
    }

    private TimeSpan GetMappedMovement(DimensionEvaluation evaluation)
    {
        var movement = TimeSpan.Zero;
        foreach (var measure in evaluation.Measures)
        {
            if (measure.Source != DimensionMeasureSource.Derived)
            {
                continue;
            }

            foreach (var mapping in mappings)
            {
                if (!mapping.DimensionId.Equals(
                        measure.DimensionId,
                        StringComparison.Ordinal) ||
                    !mapping.ValueId.Equals(
                        measure.ValueId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                movement += Multiply(measure.Duration, mapping.Multiplier);
            }
        }

        return movement;
    }

    private static TimeSpan Multiply(TimeSpan duration, decimal multiplier)
    {
        var ticks = decimal.Round(
            duration.Ticks * multiplier,
            0,
            MidpointRounding.AwayFromZero);
        if (ticks is < long.MinValue or > long.MaxValue)
        {
            throw new OverflowException("A Dimension ledger movement exceeds TimeSpan capacity.");
        }

        return TimeSpan.FromTicks(decimal.ToInt64(ticks));
    }

    private static LedgerTransaction CreateBalanced(
        Novolis.Hours.Domain.Work.WorkDayKey workDay,
        Guid sourceRegistrationId,
        TimeSpan movement,
        string reason) =>
        new(
            Guid.CreateVersion7(),
            workDay,
            sourceRegistrationId,
            [
                new DurationPosting(DurationAccount.EmployeeFlex, movement),
                new DurationPosting(DurationAccount.OrganisationControl, -movement),
            ],
            reason);

    private static TimeSpan GetAccountMovement(
        LedgerTransaction transaction,
        DurationAccount account) =>
        transaction.Postings
            .Where(posting => posting.Account == account)
            .Aggregate(
                TimeSpan.Zero,
                (total, posting) => total + posting.SignedDuration);
}
