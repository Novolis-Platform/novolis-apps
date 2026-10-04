namespace Novolis.Hours.Application;

/// <summary>Statutory working-time facts shown on Rules. Not a pay engine.</summary>
public sealed record HoursWorkingTimeNorm(
    string CountryCode,
    string Title,
    string OvertimeRule,
    string BreakRule,
    string RestRule,
    int StandardDailyMinutes,
    int WeeklyOvertimeAfterMinutes,
    int? DailyOvertimeAfterMinutes);
