using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Legal preset disclosure returned by the host.</summary>
public sealed record LegalPresetResponse(
    string Id,
    string Version,
    string CountryCode,
    string Citation,
    LegalReviewState ReviewState,
    string OvertimeAgreementMessage,
    int EmployeeSubmitBusinessDays,
    int ManagerReviewBusinessDays,
    int HrResolutionBusinessDays);
