namespace Novolis.Hours.Contracts;

/// <summary>Administrator user-creation request.</summary>
public sealed record CreateUserRequest(
    string EmployeeId,
    string Login,
    string Password,
    string DisplayName,
    HoursClientRole Role,
    string? OrganisationId = null,
    string? DivisionId = null,
    string? TeamId = null,
    int? ApprovalLevel = null,
    IReadOnlyList<HoursClientRole>? ExtraRoles = null);
