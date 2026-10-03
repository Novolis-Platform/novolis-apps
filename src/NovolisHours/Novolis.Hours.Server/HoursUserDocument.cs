using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Server;

/// <summary>JSON-persisted authorization profile linked to a Novolis Security identity.</summary>
public sealed record HoursUserDocument(
    Guid Id,
    string EmployeeId,
    string Login,
    string DisplayName,
    HoursActorRole Role,
    string? LegalPresetId = null,
    decimal WorkFraction = 1m,
    TimeOnly? ExpectedIntervalOverrideStart = null,
    TimeOnly? ExpectedIntervalOverrideEnd = null) : IHasId;
