using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client;

/// <summary>Authenticated role profile returned by the Hours HTTP API.</summary>
public sealed record HoursClientUser(
    string EmployeeId,
    string DisplayName,
    HoursClientRole Role,
    bool IsDemoAdministrator,
    IReadOnlyList<HoursClientRole>? ExtraRoles = null)
{
    /// <summary>Primary role plus extras.</summary>
    public IReadOnlyList<HoursClientRole> AssignedRoles =>
        HoursClientRoleSet.Combine(Role, ExtraRoles);

    /// <summary>Whether this person holds <paramref name="role"/>.</summary>
    public bool Has(HoursClientRole role) => HoursClientRoleSet.Has(AssignedRoles, role);
}
