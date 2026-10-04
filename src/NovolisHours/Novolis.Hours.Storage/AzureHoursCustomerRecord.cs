using Novolis.Storage.Abstractions;

namespace Novolis.Hours.Storage;

/// <summary>Scalar Azure Table row for an administrator-created workplace.</summary>
public sealed class AzureHoursCustomerRecord : IHasId
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>Workplace slug.</summary>
    public string OrganisationId { get; set; } = string.Empty;

    /// <summary>Human-readable workplace name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>ISO country code.</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>IANA or Windows time-zone id.</summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>Legal preset id.</summary>
    public string LegalPresetId { get; set; } = string.Empty;

    /// <summary>Review policy id.</summary>
    public string ReviewPolicyId { get; set; } = string.Empty;

    /// <summary>Expected work duration in invariant form.</summary>
    public string ExpectedWork { get; set; } = "07:30:00";

    /// <summary>Envelope start.</summary>
    public string EnvelopeStart { get; set; } = "07:00:00";

    /// <summary>Envelope end.</summary>
    public string EnvelopeEnd { get; set; } = "17:00:00";

    /// <summary>Core-hours start.</summary>
    public string CoreHoursStart { get; set; } = "09:00:00";

    /// <summary>Core-hours end.</summary>
    public string CoreHoursEnd { get; set; } = "15:00:00";

    /// <summary>Morning routine start.</summary>
    public string RoutineMorningStart { get; set; } = "08:00:00";

    /// <summary>Morning routine end.</summary>
    public string RoutineMorningEnd { get; set; } = "11:30:00";

    /// <summary>Afternoon routine start.</summary>
    public string RoutineAfternoonStart { get; set; } = "12:30:00";

    /// <summary>Afternoon routine end.</summary>
    public string RoutineAfternoonEnd { get; set; } = "16:30:00";

    /// <summary>Whether national holidays are observed.</summary>
    public bool ObservesPublicHolidays { get; set; } = true;

    /// <summary>Whether every weekday including Sunday is a working day.</summary>
    public bool SevenDayOperation { get; set; }

    /// <summary>Whether Saturday is a working day.</summary>
    public bool SaturdayIsWorkingDay { get; set; }

    /// <summary>Whether flex saldo exists.</summary>
    public bool AllowsFlex { get; set; } = true;

    /// <summary>Whether employees may dispute.</summary>
    public bool AllowsDispute { get; set; } = true;

    /// <summary>Whether the employee only confirms attendance.</summary>
    public bool AttendanceConfirmationOnly { get; set; }

    /// <summary>Creates a scalar Azure row from the persisted document.</summary>
    public static AzureHoursCustomerRecord FromDocument(HoursCustomerDocument document) =>
        new()
        {
            Id = document.Id,
            OrganisationId = document.OrganisationId,
            DisplayName = document.DisplayName,
            CountryCode = document.CountryCode,
            TimeZoneId = document.TimeZoneId,
            LegalPresetId = document.LegalPresetId,
            ReviewPolicyId = document.ReviewPolicyId,
            ExpectedWork = document.ExpectedWork,
            EnvelopeStart = document.EnvelopeStart,
            EnvelopeEnd = document.EnvelopeEnd,
            CoreHoursStart = document.CoreHoursStart,
            CoreHoursEnd = document.CoreHoursEnd,
            RoutineMorningStart = document.RoutineMorningStart,
            RoutineMorningEnd = document.RoutineMorningEnd,
            RoutineAfternoonStart = document.RoutineAfternoonStart,
            RoutineAfternoonEnd = document.RoutineAfternoonEnd,
            ObservesPublicHolidays = document.ObservesPublicHolidays,
            SevenDayOperation = document.SevenDayOperation,
            SaturdayIsWorkingDay = document.SaturdayIsWorkingDay,
            AllowsFlex = document.AllowsFlex,
            AllowsDispute = document.AllowsDispute,
            AttendanceConfirmationOnly = document.AttendanceConfirmationOnly,
        };

    /// <summary>Rehydrates the persisted document.</summary>
    public HoursCustomerDocument ToDocument() =>
        new(
            Id,
            OrganisationId,
            DisplayName,
            CountryCode,
            TimeZoneId,
            LegalPresetId,
            ReviewPolicyId,
            ExpectedWork,
            EnvelopeStart,
            EnvelopeEnd,
            CoreHoursStart,
            CoreHoursEnd,
            RoutineMorningStart,
            RoutineMorningEnd,
            RoutineAfternoonStart,
            RoutineAfternoonEnd,
            ObservesPublicHolidays,
            SevenDayOperation,
            SaturdayIsWorkingDay,
            AllowsFlex,
            AllowsDispute,
            AttendanceConfirmationOnly);
}
