using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>JSON-persisted workplace created by an administrator.</summary>
public sealed record HoursCustomerDocument(
    Guid Id,
    string OrganisationId,
    string DisplayName,
    string CountryCode,
    string TimeZoneId,
    string LegalPresetId,
    string ReviewPolicyId,
    string ExpectedWork,
    string EnvelopeStart,
    string EnvelopeEnd,
    string CoreHoursStart,
    string CoreHoursEnd,
    string RoutineMorningStart,
    string RoutineMorningEnd,
    string RoutineAfternoonStart,
    string RoutineAfternoonEnd,
    bool ObservesPublicHolidays,
    bool SevenDayOperation,
    bool SaturdayIsWorkingDay,
    bool AllowsFlex,
    bool AllowsDispute,
    bool AttendanceConfirmationOnly) : IHasId;
