using System.Collections.Immutable;

namespace Novolis.Hours.Contracts;

/// <summary>Wire representation of the configuration used to interpret a WorkDay.</summary>
public sealed record ConfigurationSnapshotResponse(
    Guid Id,
    string CalendarVersion,
    string DimensionVersion,
    string ComplianceVersion,
    string WorkflowVersion,
    string LedgerVersion,
    string TimeZoneId);

/// <summary>One applied DayShape contribution shown in the explanation view.</summary>
public sealed record AppliedDayRuleResponse(
    string RuleId,
    string CalendarId,
    string CalendarVersion,
    int Order,
    string RuleType,
    string Summary,
    string Source,
    string LayerKind,
    string? Jurisdiction,
    string? HolidayId,
    string? SourcePackage,
    string? SourcePackageVersion,
    string? GeneratorVersion);

/// <summary>Neutral comparison between observed and configured routine time.</summary>
public sealed record RoutineDifferenceResponse(
    string Kind,
    TimeSpan Duration,
    string Explanation);

/// <summary>One derived or manually assigned Dimension measure.</summary>
public sealed record DimensionMeasureResponse(
    string DimensionId,
    string ValueId,
    TimeSpan Duration,
    WorkIntervalRequest? Interval,
    string Source,
    string? RuleId);

/// <summary>One non-blocking factual compliance indicator.</summary>
public sealed record ComplianceIndicatorResponse(
    string Code,
    string Category,
    string Message,
    string EmployeeId,
    DateOnly NominalDate,
    WorkIntervalRequest? AffectedInterval,
    string RuleId);

/// <summary>One immutable duration posting.</summary>
public sealed record LedgerPostingResponse(string Account, TimeSpan SignedDuration);

/// <summary>One balanced ledger transaction retained in the audit trail.</summary>
public sealed record LedgerTransactionResponse(
    Guid Id,
    string EmployeeId,
    DateOnly NominalDate,
    Guid SourceRegistrationId,
    ImmutableArray<LedgerPostingResponse> Postings,
    string Reason);

/// <summary>Complete explainable WorkDay projection.</summary>
public sealed record WorkDayResponse(
    string EmployeeId,
    string OrganisationId,
    DateOnly NominalDate,
    bool IsWorkingDay,
    TimeSpan ExpectedWork,
    TimeSpan PaidEntitlement,
    string? WorkEnvelope,
    ImmutableArray<string> CoreHours,
    ImmutableArray<string> RoutineWork,
    ImmutableArray<string> Tags,
    ImmutableArray<AppliedDayRuleResponse> AppliedRules,
    WorkRegistrationResponse? Registration,
    ImmutableArray<WorkIntervalRequest> WorkedIntervals,
    TimeSpan ActualWorked,
    TimeSpan OutsideRoutine,
    TimeSpan MissingRoutine,
    ImmutableArray<RoutineDifferenceResponse> RoutineDifferences,
    ImmutableArray<DimensionMeasureResponse> Dimensions,
    ImmutableArray<string> MissingDimensionCoverage,
    ImmutableArray<ComplianceIndicatorResponse> ComplianceIndicators,
    ImmutableArray<LedgerTransactionResponse> LedgerTransactions,
    ConfigurationSnapshotResponse Configuration,
    LocalTimeRangeDto? WorkEnvelopeRange,
    ImmutableArray<LocalTimeRangeDto> CoreHourRanges,
    ImmutableArray<LocalTimeRangeDto> RoutineRanges,
    ImmutableArray<DimensionBrushResponse> BrushCatalog,
    bool AllowsFlex = true,
    bool AllowsDispute = true,
    bool AttendanceConfirmationOnly = false,
    string OrganisationName = "");

/// <summary>Review action returned with actor and approval provenance.</summary>
public sealed record ReviewActionResponse(
    Guid Id,
    Guid PeriodId,
    string Kind,
    string ActorId,
    string ActorRole,
    int? ApprovalLevel,
    string? Comment,
    DateTimeOffset RecordedAt);

/// <summary>One configured review stage and its current state.</summary>
public sealed record ReviewStageResponse(
    string Id,
    string RequiredAction,
    string Role,
    int? ApprovalLevel,
    DateOnly DueDate,
    bool IsComplete,
    bool IsOverdue,
    Guid? SatisfiedBy);

/// <summary>Review projection reconstructed from period and action events.</summary>
public sealed record ReviewProjectionResponse(
    Guid PeriodId,
    string EmployeeId,
    DateOnly From,
    DateOnly Through,
    string PolicyId,
    string State,
    bool ChangedAfterApproval,
    ImmutableArray<ReviewActionResponse> Actions,
    ImmutableArray<ReviewStageResponse> Stages,
    ImmutableArray<string> Anomalies,
    ImmutableArray<string> Escalations,
    Guid? JournalHead,
    bool AllowsDispute = true,
    bool AttendanceConfirmationOnly = false);

/// <summary>Creates a review period for an employee.</summary>
public sealed record CreateReviewPeriodRequest(
    string EmployeeId,
    DateOnly From,
    DateOnly Through);

/// <summary>Appends one review action without mutating prior history.</summary>
public sealed record RecordReviewActionRequest(
    string Kind,
    int? ApprovalLevel,
    string? Comment,
    Guid? ExpectedJournalHead);

/// <summary>One health-concern aggregate with WorkDay drill-down keys.</summary>
public sealed record HealthConcernRowResponse(
    string Code,
    string Category,
    int IndicatorCount,
    TimeSpan AffectedDuration,
    ImmutableArray<string> WorkDays);

/// <summary>Organisation-level health report without person ranking.</summary>
public sealed record HealthConcernsReportResponse(
    ImmutableArray<HealthConcernRowResponse> Rows,
    string Description);

/// <summary>One customer/project overlap row for extra-work attribution.</summary>
public sealed record BusinessPressureRowResponse(
    string DimensionId,
    string ValueId,
    TimeSpan Duration,
    ImmutableArray<string> WorkDays);

/// <summary>Business-pressure report using temporal overlap language.</summary>
public sealed record BusinessPressureReportResponse(
    ImmutableArray<BusinessPressureRowResponse> Rows,
    string Description);
