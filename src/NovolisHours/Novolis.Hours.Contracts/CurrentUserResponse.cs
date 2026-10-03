using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Current authenticated product profile.</summary>
public sealed record CurrentUserResponse(string EmployeeId, string DisplayName, HoursActorRole Role);
