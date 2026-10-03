namespace Novolis.Hours.Domain;

/// <summary>Captures the policy identifiers that were effective when a record was made.</summary>
public sealed record HoursWorktimeSnapshot(
    string ProfileId,
    string TemplateId,
    string CalendarId,
    string CalendarSource,
    string LegalPresetId,
    string LegalPresetVersion);
