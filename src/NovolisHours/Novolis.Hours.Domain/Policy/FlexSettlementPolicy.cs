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

    /// <summary>Gets the latest completed settlement boundary before an assessment day, or null for manual settlements.</summary>
    public DateOnly? MostRecentClosedDate(DateOnly assessedOn) =>
        Cadence switch
        {
            FlexSettlementCadence.Manual => null,
            FlexSettlementCadence.Monthly => new DateOnly(assessedOn.Year, assessedOn.Month, 1).AddDays(-1),
            FlexSettlementCadence.Quarterly => new DateOnly(
                    assessedOn.Year,
                    ((assessedOn.Month - 1) / 3) * 3 + 1,
                    1)
                .AddDays(-1),
            FlexSettlementCadence.Annual when assessedOn.Year > 1 => new DateOnly(assessedOn.Year - 1, 12, 31),
            FlexSettlementCadence.Annual => null,
            _ => throw new ArgumentOutOfRangeException(nameof(Cadence), Cadence, "Unknown settlement cadence."),
        };

    private static DateOnly QuarterEnd(DateOnly day)
    {
        var month = ((day.Month - 1) / 3 + 1) * 3;
        return new DateOnly(day.Year, month, DateTime.DaysInMonth(day.Year, month));
    }
}
