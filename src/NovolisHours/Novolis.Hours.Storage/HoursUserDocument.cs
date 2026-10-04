using Novolis.Hours.Domain;
using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

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
    TimeOnly? ExpectedIntervalOverrideEnd = null,
    string OrganisationId = "norway-org",
    string? DivisionId = null,
    string? TeamId = null,
    int? ApprovalLevel = null,
    IReadOnlyList<HoursActorRole>? ExtraRoles = null) : IHasId
{
    /// <summary>Primary role plus extras. The journal still stamps <see cref="Role"/>.</summary>
    public IReadOnlyList<HoursActorRole> AssignedRoles =>
        HoursRoleSet.Combine(Role, ExtraRoles);

    /// <summary>Whether this person holds <paramref name="role"/>.</summary>
    public bool Has(HoursActorRole role) => HoursRoleSet.Has(AssignedRoles, role);
}
