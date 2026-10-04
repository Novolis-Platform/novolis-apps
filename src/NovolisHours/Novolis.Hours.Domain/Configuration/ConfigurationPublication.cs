namespace Novolis.Hours.Domain.Configuration;

/// <summary>Append-only publication of a configuration snapshot from an effective nominal date.</summary>
public sealed record ConfigurationPublication(
    string EmployeeId,
    DateOnly EffectiveFrom,
    ConfigurationSnapshot Snapshot);
