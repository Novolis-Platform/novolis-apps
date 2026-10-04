using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Flex normalization assessment request.</summary>
public sealed record ProposeFlexNormalizationRequest(string EmployeeId, DateOnly AssessedThrough);
