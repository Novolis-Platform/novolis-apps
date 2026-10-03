namespace Novolis.Hours.Client;

/// <summary>Period of actual presence marked for financial compensation outside Novolis Hours.</summary>
public sealed record HoursFinancialCompensationSlice(TimeOnly StartedAt, TimeOnly EndedAt);
