namespace Novolis.Hours.Domain.Configuration;

/// <summary>Stable explanation reference for a derived rule result.</summary>
public sealed record RuleRef(
    string RuleId,
    string Version,
    RuleSource Source,
    string? PackageId = null,
    string? PackageVersion = null);
