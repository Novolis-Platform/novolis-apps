using Novolis.Hours.Domain;
using Novolis.Time.Worktime;
using Novolis.Time.Worktime.Legal;

namespace Novolis.Hours.Contracts;

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
