using Novolis.Time.Workday;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Domain;

/// <summary>Versioned configuration that connects a profile, calendar, template, and legal preset.</summary>
public sealed record HoursPolicy(
    string Id,
    EmploymentSettings EmploymentSettings,
    WorktimeLegalPreset LegalPreset,
    IWorkdayCalendar Calendar,
    FlexSettlementPolicy SettlementPolicy);
