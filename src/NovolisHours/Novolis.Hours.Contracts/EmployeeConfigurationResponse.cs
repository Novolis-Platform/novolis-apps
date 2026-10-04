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
