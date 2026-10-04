namespace Novolis.Hours.Contracts;

/// <summary>Explains a proposed adjustment on the Hours wire.</summary>
public enum HoursAdjustmentReason
{
    ConvertToFinancialCompensation,
    NotOnClock,
    Correction,
    FlexNormalization,
    Other,
}
