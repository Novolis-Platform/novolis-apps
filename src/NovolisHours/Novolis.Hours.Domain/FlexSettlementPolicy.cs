namespace Novolis.Hours.Domain;

/// <summary>Configures the assessment cadence that can propose employee-approved flex normalization.</summary>
public sealed record FlexSettlementPolicy(
    string Id,
    FlexSettlementCadence Cadence,
    bool RequiresEmployeeAcceptance)
{
    /// <summary>Gets the calendar close date containing the supplied day.</summary>
    public DateOnly ClosingDate(DateOnly day) =>
        Cadence switch
        {
            FlexSettlementCadence.Monthly => new DateOnly(day.Year, day.Month, DateTime.DaysInMonth(day.Year, day.Month)),
            FlexSettlementCadence.Quarterly => QuarterEnd(day),
            FlexSettlementCadence.Annual => new DateOnly(day.Year, 12, 31),
            FlexSettlementCadence.Manual => day,
            _ => throw new ArgumentOutOfRangeException(nameof(Cadence), Cadence, "Unknown settlement cadence."),
        };

    private static DateOnly QuarterEnd(DateOnly day)
    {
        var month = ((day.Month - 1) / 3 + 1) * 3;
        return new DateOnly(day.Year, month, DateTime.DaysInMonth(day.Year, month));
    }
}
