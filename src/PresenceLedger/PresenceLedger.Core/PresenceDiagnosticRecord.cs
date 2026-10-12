namespace PresenceLedger.Core;

/// <summary>One operational entry kept separately from raw presence observations.</summary>
public sealed record PresenceDiagnosticRecord
{
    /// <summary>Current serialized diagnostic schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Creates an operational diagnostic record.</summary>
    [System.Text.Json.Serialization.JsonConstructor]
    public PresenceDiagnosticRecord(
        DateTimeOffset at,
        string level,
        string code,
        string message,
        string? detail = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (at == default)
            throw new ArgumentException("Diagnostic time must be set.", nameof(at));
        ArgumentException.ThrowIfNullOrWhiteSpace(level);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (schemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));

        At = at.ToUniversalTime();
        Level = level.Trim();
        Code = code.Trim();
        Message = message.Trim();
        Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        SchemaVersion = schemaVersion;
    }

    /// <summary>UTC time at which the diagnostic was recorded.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Severity such as info, warning, or error.</summary>
    public string Level { get; }

    /// <summary>Stable operational code for filtering exported diagnostics.</summary>
    public string Code { get; }

    /// <summary>Short human-readable diagnostic message.</summary>
    public string Message { get; }

    /// <summary>Optional exception or platform detail.</summary>
    public string? Detail { get; }

    /// <summary>Schema version used for the serialized record.</summary>
    public int SchemaVersion { get; }
}
