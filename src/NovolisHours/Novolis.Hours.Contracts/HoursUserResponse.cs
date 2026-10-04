namespace Novolis.Hours.Contracts;

/// <summary>Product profile returned to an administrator.</summary>
public sealed record HoursUserResponse(
    string EmployeeId,
    string Login,
    string DisplayName,
    HoursClientRole Role,
    string OrganisationId,
    string? DivisionId,
    string? TeamId,
    int? ApprovalLevel,
    IReadOnlyList<HoursClientRole>? ExtraRoles = null)
{
    /// <summary>Primary role plus extras.</summary>
    public IReadOnlyList<HoursClientRole> AssignedRoles =>
        HoursClientRoleSet.Combine(Role, ExtraRoles);
}
