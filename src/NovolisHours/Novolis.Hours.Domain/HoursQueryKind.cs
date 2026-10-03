namespace Novolis.Hours.Domain;

/// <summary>Named read-model slices exposed by the Hours query engine.</summary>
public enum HoursQueryKind
{
    /// <summary>Returns saldo and record-count summary data.</summary>
    Summary,

    /// <summary>Returns recorded actual presence rows.</summary>
    Entries,

    /// <summary>Returns ISO-week rollups of recorded expected, actual, and flex duration.</summary>
    Weekly,

    /// <summary>Returns proposed and resolved adjustment rows.</summary>
    Adjustments,

    /// <summary>Returns legal and workflow anomaly rows.</summary>
    Anomalies,

    /// <summary>Returns balanced duration-ledger posting rows.</summary>
    Ledger,
}
