namespace Novolis.Hours.Contracts;

/// <summary>Publishes the effective configuration snapshot for an employee from a nominal date.</summary>
public sealed record PublishConfigurationRequest(DateOnly EffectiveFrom);
