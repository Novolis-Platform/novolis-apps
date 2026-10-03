using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Employee-specific worktime overrides.</summary>
public sealed record IndividualSettingsResponse(
    decimal WorkFraction,
    TimeOnly? ExpectedIntervalOverrideStart,
    TimeOnly? ExpectedIntervalOverrideEnd);
