using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client;

/// <summary>Authenticated role profile returned by the Hours HTTP API.</summary>
public sealed record HoursClientUser(
    string EmployeeId,
    string DisplayName,
    HoursClientRole Role,
    bool IsDemoAdministrator);
