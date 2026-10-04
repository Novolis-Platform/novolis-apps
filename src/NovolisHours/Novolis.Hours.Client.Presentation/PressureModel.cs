using Novolis.Hours.Contracts;

namespace Novolis.Hours.Client.Presentation;

/// <summary>Business-pressure totals from the organisation report.</summary>
public sealed class PressureModel
{
    /// <summary>Creates pressure totals from the wire report.</summary>
    public PressureModel(BusinessPressureReportResponse report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Report = report;
        Total = report.Rows.Aggregate(TimeSpan.Zero, (sum, row) => sum + row.Duration);
        Unattributed = report.Rows
            .Where(row => string.Equals(row.ValueId, "Unattributed", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(row.ValueId, "unattributed", StringComparison.Ordinal))
            .Aggregate(TimeSpan.Zero, (sum, row) => sum + row.Duration);
        Attributed = Total - Unattributed;
    }

    /// <summary>Server report.</summary>
    public BusinessPressureReportResponse Report { get; }

    /// <summary>All extra-work duration.</summary>
    public TimeSpan Total { get; }

    /// <summary>Painted extra-work duration.</summary>
    public TimeSpan Attributed { get; }

    /// <summary>Unpainted extra-work duration.</summary>
    public TimeSpan Unattributed { get; }
}
