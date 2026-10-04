using System.Security.Cryptography;
using System.Text;
using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Compliance;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Dimensions;
using Novolis.Hours.Domain.Ledger;
using Novolis.Hours.Domain.Review;

namespace Novolis.Hours.Application;

/// <summary>
/// Deterministic development configuration used by the acceptance regime.
/// The same employee and nominal date always resolve to the same snapshot and
/// calendar explanation, including after a process restart.
/// </summary>
public sealed class HoursAcceptanceConfiguration
{
    /// <summary>Gets the effective snapshot for one employee and nominal date.</summary>
    public ConfigurationSnapshot GetSnapshot(string employeeId, DateOnly date)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        var calendarVersion = HoursCustomerCatalog.GetCalendarVersion(employeeId, date);
        var snapshotKey = string.Join(
            "|",
            employeeId,
            date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            calendarVersion);
        return new ConfigurationSnapshot(
            StableId(snapshotKey),
            calendarVersion,
            "acceptance.dimensions.v1",
            "acceptance.compliance.v1",
            HoursReviewWorkflowCatalog.WorkflowVersion(employeeId),
            "acceptance.ledger.v1",
            HoursCustomerCatalog.TimeZoneId(employeeId));
    }

    /// <summary>Gets the snapshot selected by the effective publication history.</summary>
    public ConfigurationSnapshot GetSnapshot(
        string employeeId,
        DateOnly date,
        IEnumerable<ConfigurationPublication> publications)
    {
        ArgumentNullException.ThrowIfNull(publications);
        return publications
            .Where(publication =>
                publication.EmployeeId.Equals(employeeId, StringComparison.Ordinal) &&
                publication.EffectiveFrom <= date)
            .OrderByDescending(publication => publication.EffectiveFrom)
            .ThenByDescending(publication => publication.Snapshot.Id.Value)
            .Select(publication => publication.Snapshot)
            .FirstOrDefault() ?? GetSnapshot(employeeId, date);
    }

    /// <summary>Creates the deterministic snapshot emitted by a future configuration publication.</summary>
    public ConfigurationSnapshot CreatePublishedSnapshot(string employeeId, DateOnly effectiveFrom)
    {
        var baseline = GetSnapshot(employeeId, effectiveFrom);
        var calendarVersion = $"acceptance-calendar-published-{effectiveFrom:yyyyMMdd}";
        return new ConfigurationSnapshot(
            StableId($"{employeeId}|{effectiveFrom:yyyy-MM-dd}|{calendarVersion}"),
            calendarVersion,
            baseline.DimensionVersion,
            baseline.ComplianceVersion,
            baseline.WorkflowVersion,
            baseline.LedgerVersion,
            baseline.TimeZoneId);
    }

    /// <summary>Gets the ordered calendar stack used to explain one day.</summary>
    public WorkCalendarStack GetCalendar(string employeeId, DateOnly date) =>
        GetCalendar(employeeId, date, calendarVersionOverride: null);

    /// <summary>Gets a calendar stack, optionally relabelled by a published snapshot.</summary>
    public WorkCalendarStack GetCalendar(
        string employeeId,
        DateOnly date,
        string? calendarVersionOverride)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        return HoursCustomerCatalog.GetCalendar(employeeId, date, calendarVersionOverride);
    }

    /// <summary>Gets the sparse Dimensions used by the acceptance projection.</summary>
    public DimensionConfiguration GetDimensions(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        var flexRule = new DimensionRuleRegistration(
            "routine-difference-to-flex",
            100,
            new RuleRef("routine-difference-to-flex", "1", RuleSource.Manual),
            new RoutineDifferenceDimensionRule("time-type", "flex-credit", null));
        DimensionDefinition[] definitions =
        [
            new DimensionDefinition(
                "time-type",
                "Time type",
                DimensionAssignmentMode.DerivedAndManual,
                DimensionCardinality.Additive,
                false,
                [
                    new DimensionValue("flex-credit", "Flex credit"),
                    new DimensionValue("overtime", "Overtime"),
                    new DimensionValue("routine", "Routine"),
                ]),
            new DimensionDefinition(
                "customer",
                "Customer",
                DimensionAssignmentMode.Manual,
                DimensionCardinality.Exclusive,
                false,
                [
                    new DimensionValue("acme", "ACME"),
                    new DimensionValue("beta", "BETA"),
                    new DimensionValue("unattributed", "Unattributed"),
                ]),
            new DimensionDefinition(
                "project",
                "Project",
                DimensionAssignmentMode.Manual,
                DimensionCardinality.Exclusive,
                false,
                [
                    new DimensionValue("phoenix", "Phoenix"),
                    new DimensionValue("support", "Support"),
                    new DimensionValue("internal-platform", "Internal Platform"),
                ]),
            new DimensionDefinition(
                "billability",
                "Billability",
                DimensionAssignmentMode.Manual,
                DimensionCardinality.Exclusive,
                false,
                [
                    new DimensionValue("billable", "Billable"),
                    new DimensionValue("internal", "Internal"),
                ])
        ];

        return new DimensionConfiguration(
        [
            new DimensionConfigurationLayer(
                "organisation",
                "acceptance.dimensions.organisation.v1",
                10,
                definitions,
                [flexRule],
                RuleSource.Manual),
        ]);
    }

    /// <summary>Gets the non-blocking compliance rules used for one employee.</summary>
    public IReadOnlyList<ComplianceRuleRegistration> GetComplianceRules(string employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        return
        [
            new ComplianceRuleRegistration(
                "long-workday",
                10,
                new RuleRef("long-workday", "1", RuleSource.Manual),
                new LongWorkdayComplianceRule(TimeSpan.FromHours(8), new RuleRef("long-workday", "1", RuleSource.Manual))),
            new ComplianceRuleRegistration(
                "night-work",
                20,
                new RuleRef("night-work", "1", RuleSource.Manual),
                new NightWorkComplianceRule(new TimeOnly(22, 0), new TimeOnly(6, 0), new RuleRef("night-work", "1", RuleSource.Manual))),
            new ComplianceRuleRegistration(
                "daily-rest",
                25,
                new RuleRef("daily-rest", "1", RuleSource.Manual),
                new InsufficientDailyRestComplianceRule(
                    TimeSpan.FromHours(11),
                    new RuleRef("daily-rest", "1", RuleSource.Manual))),
            new ComplianceRuleRegistration(
                "weekend-work",
                30,
                new RuleRef("weekend-work", "1", RuleSource.Manual),
                new WeekendWorkComplianceRule(new RuleRef("weekend-work", "1", RuleSource.Manual))),
        ];
    }

    /// <summary>Gets the review policy that applies to one employee's customer.</summary>
    public ReviewPolicy GetReviewPolicy(string employeeId) =>
        HoursReviewWorkflowCatalog.ForEmployee(employeeId);

    /// <summary>Gets the explicit mapping from derived flex meaning to duration accounts.</summary>
    public LedgerProjector GetLedgerProjector() =>
        new([new DimensionLedgerMapping("time-type", "flex-credit", 1m)]);

    private static ConfigurationSnapshotId StableId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new ConfigurationSnapshotId(new Guid(bytes[..16]));
    }
}
