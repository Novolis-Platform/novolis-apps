namespace PresenceLedger.App;

/// <summary>Sends a plain-text presence report off the device.</summary>
public interface IReportExport
{
    /// <summary>Opens the host share sheet for the report text.</summary>
    Task ExportAsync(string report, CancellationToken cancellationToken = default);
}
