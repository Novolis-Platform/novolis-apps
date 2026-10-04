using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Administrator worktime settings request.</summary>
public sealed record UpdateWorktimeSettingsRequest(
    string? LegalPresetId,
    decimal WorkFraction,
    TimeOnly? ExpectedIntervalOverrideStart,
    TimeOnly? ExpectedIntervalOverrideEnd);
