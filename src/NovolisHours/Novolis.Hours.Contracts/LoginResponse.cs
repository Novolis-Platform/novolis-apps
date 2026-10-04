namespace Novolis.Hours.Contracts;

/// <summary>Credential login response.</summary>
public sealed record LoginResponse(
    string EmployeeId,
    string DisplayName,
    HoursClientRole Role,
    bool IsDemoAdministrator);
