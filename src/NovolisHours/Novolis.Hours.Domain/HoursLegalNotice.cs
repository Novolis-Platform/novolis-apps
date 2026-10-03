namespace Novolis.Hours.Domain;

/// <summary>Immutable informational rule message retained with the worktime record.</summary>
public sealed record HoursLegalNotice(
    string RuleId,
    string Message,
    string Citation,
    string PresetId,
    string PresetVersion);
