namespace Novolis.Hours.Domain;

/// <summary>Resolution selected by HR or Higher for an employee-disputed adjustment.</summary>
public enum HoursAdjustmentResolution
{
    /// <summary>The reviewed adjustment is committed to the duration ledger.</summary>
    Accept,

    /// <summary>The reviewed adjustment remains in the audit trail but does not affect saldo.</summary>
    Reject,
}
