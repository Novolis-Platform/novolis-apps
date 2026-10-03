using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

/// <summary>Stable JSON API contracts shared by the Hours host and clients.</summary>
public sealed record AntiforgeryResponse(string Token);

/// <summary>Credential login request.</summary>
public sealed record LoginRequest(string Login, string Password);

/// <summary>Authenticated password-change request.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Credential login response.</summary>
public sealed record LoginResponse(
    string EmployeeId,
    string DisplayName,
    HoursActorRole Role,
    bool IsDemoAdministrator);

/// <summary>Current authenticated product profile.</summary>
public sealed record CurrentUserResponse(string EmployeeId, string DisplayName, HoursActorRole Role);

/// <summary>Effective employee worktime configuration.</summary>
public sealed record EmployeeConfigurationResponse(
    string EmployeeId,
    ProfileResponse Profile,
    TemplateResponse Template,
    IndividualSettingsResponse IndividualSettings,
    CalendarResponse Calendar,
    LegalPresetResponse LegalPreset,
    string SettlementPolicyId,
    FlexSettlementCadence SettlementCadence);

/// <summary>Effective immutable profile details.</summary>
public sealed record ProfileResponse(
    string Id,
    string Name,
    TimeOnly WorkingDayStart,
    TimeOnly WorkingDayEnd,
    TimeOnly CoreStart,
    TimeOnly CoreEnd,
    TimeOnly LunchStart,
    TimeOnly LunchEnd,
    TimeSpan WeekHours,
    TimeSpan DayHours,
    PresenceClassification UnmarkedSurplusClassification);

/// <summary>Working-time template identity.</summary>
public sealed record TemplateResponse(string Id, string Name);

/// <summary>Employee-specific worktime overrides.</summary>
public sealed record IndividualSettingsResponse(
    decimal WorkFraction,
    TimeOnly? ExpectedIntervalOverrideStart,
    TimeOnly? ExpectedIntervalOverrideEnd);

/// <summary>Calendar identity used by the effective policy.</summary>
public sealed record CalendarResponse(string Id, string Source, string Version, string CountryCode);

/// <summary>Actual-presence registration request.</summary>
public sealed record RegisterWorkRequest(
    string EmployeeId,
    DateOnly Day,
    TimeOnly StartedAt,
    TimeOnly EndedAt,
    TimeOnly? BreakStartedAt,
    TimeOnly? BreakEndedAt,
    IReadOnlyCollection<FinancialCompensationSlice>? FinancialCompensationSlices,
    string Comment,
    bool ManagerAgreementRecorded);

/// <summary>Proposed immutable adjustment request.</summary>
public sealed record ProposeAdjustmentRequest(
    string EmployeeId,
    DateOnly EffectiveDay,
    TimeSpan DurationDelta,
    HoursAdjustmentReason Reason,
    string Comment);

/// <summary>Flex normalization assessment request.</summary>
public sealed record ProposeFlexNormalizationRequest(string EmployeeId, DateOnly AssessedThrough);

/// <summary>Employee adjustment response request.</summary>
public sealed record RespondToAdjustmentRequest(HoursAdjustmentResponse Response, string Comment);

/// <summary>Escalated adjustment resolution request.</summary>
public sealed record ResolveAdjustmentRequest(HoursAdjustmentResolution Resolution, string Comment);

/// <summary>Approval-period opening request.</summary>
public sealed record OpenApprovalPeriodRequest(string EmployeeId, DateOnly StartsOn, DateOnly EndsOn);

/// <summary>Approval-period action request.</summary>
public sealed record ApprovalActionRequest(string Comment);

/// <summary>Administrator user-creation request.</summary>
public sealed record CreateUserRequest(
    string EmployeeId,
    string Login,
    string Password,
    string DisplayName,
    HoursActorRole Role);

/// <summary>Administrator worktime settings request.</summary>
public sealed record UpdateWorktimeSettingsRequest(
    string? LegalPresetId,
    decimal WorkFraction,
    TimeOnly? ExpectedIntervalOverrideStart,
    TimeOnly? ExpectedIntervalOverrideEnd);

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
