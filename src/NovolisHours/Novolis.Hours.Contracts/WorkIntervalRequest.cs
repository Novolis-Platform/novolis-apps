namespace Novolis.Hours.Contracts;

/// <summary>Wire interval using absolute timestamps.</summary>
public sealed record WorkIntervalRequest(
    DateTimeOffset Start,
    DateTimeOffset End);
