namespace Novolis.Hours.Domain;

/// <summary>Identifies presence that should be financially compensated without calculating any payment.</summary>
public sealed record FinancialCompensationSlice(TimeOnly Start, TimeOnly End, string Reason)
{
    /// <summary>Gets the recorded duration.</summary>
    public TimeSpan Duration => End - Start;
}
