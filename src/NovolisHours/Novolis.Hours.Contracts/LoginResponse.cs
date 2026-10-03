using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Credential login response.</summary>
public sealed record LoginResponse(
    string EmployeeId,
    string DisplayName,
    HoursActorRole Role,
    bool IsDemoAdministrator);
