namespace Novolis.Hours.Domain.Configuration;

/// <summary>Immutable versions used together to interpret one recorded assertion.</summary>
public sealed record ConfigurationSnapshot
{
    /// <summary>Initializes a configuration snapshot.</summary>
    public ConfigurationSnapshot(
        ConfigurationSnapshotId id,
        string calendarVersion,
        string dimensionVersion,
        string complianceVersion,
        string workflowVersion,
        string ledgerVersion,
        string timeZoneId)
    {
        id.EnsureAssigned();
        ArgumentException.ThrowIfNullOrWhiteSpace(calendarVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(complianceVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(ledgerVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        Id = id;
        CalendarVersion = calendarVersion;
        DimensionVersion = dimensionVersion;
        ComplianceVersion = complianceVersion;
        WorkflowVersion = workflowVersion;
        LedgerVersion = ledgerVersion;
        TimeZoneId = timeZoneId;
    }

    /// <summary>Snapshot identity.</summary>
    public ConfigurationSnapshotId Id { get; }

    /// <summary>Effective calendar stack version.</summary>
    public string CalendarVersion { get; }

    /// <summary>Effective Dimension configuration version.</summary>
    public string DimensionVersion { get; }

    /// <summary>Effective compliance rule version.</summary>
    public string ComplianceVersion { get; }

    /// <summary>Effective review/workflow policy version.</summary>
    public string WorkflowVersion { get; }

    /// <summary>Effective ledger mapping version.</summary>
    public string LedgerVersion { get; }

    /// <summary>Time-zone identifier used for local routine interpretation.</summary>
    public string TimeZoneId { get; }
}
