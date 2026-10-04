namespace Novolis.Hours.Contracts;

/// <summary>Current authenticated product profile.</summary>
public sealed record CurrentUserResponse(
    string EmployeeId,
    string DisplayName,
    HoursClientRole Role,
    IReadOnlyList<HoursClientRole>? ExtraRoles = null);
