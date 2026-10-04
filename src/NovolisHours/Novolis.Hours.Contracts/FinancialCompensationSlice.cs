namespace Novolis.Hours.Contracts;

/// <summary>Financially compensated interval on the Hours wire.</summary>
public sealed record FinancialCompensationSlice(TimeOnly Start, TimeOnly End, string Reason);
