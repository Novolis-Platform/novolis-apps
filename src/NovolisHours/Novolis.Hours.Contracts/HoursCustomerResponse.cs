namespace Novolis.Hours.Contracts;

/// <summary>Workplace returned to an administrator.</summary>
public sealed record HoursCustomerResponse(
    string Id,
    string DisplayName,
    string CountryCode,
    string TimeZoneId,
    string LegalPresetId,
    string ReviewPolicyId,
    TimeSpan ExpectedWork,
    LocalTimeRangeDto Envelope,
    LocalTimeRangeDto CoreHours,
    bool SaturdayIsWorkingDay,
    bool AllowsFlex,
    bool AllowsDispute,
    bool AttendanceConfirmationOnly,
    bool BuiltIn);
