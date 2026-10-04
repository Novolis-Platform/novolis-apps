namespace Novolis.Hours.Contracts;

/// <summary>Administrator user-creation request.</summary>
public sealed record CreateUserRequest(
    string EmployeeId,
    string Login,
    string Password,
    string DisplayName,
    HoursClientRole Role);
