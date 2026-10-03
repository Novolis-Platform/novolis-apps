namespace Novolis.Hours.Domain;

/// <summary>Tracks employee consent and escalation for a proposed duration adjustment.</summary>
public enum HoursAdjustmentState
{
    /// <summary>The adjustment exists but does not affect saldo until the employee responds.</summary>
    AwaitingEmployee,

    /// <summary>The employee accepted the adjustment and it is committed to the duration ledger.</summary>
    Accepted,

    /// <summary>The employee disputed the adjustment and it has been escalated to HR or Higher.</summary>
    Escalated,

    /// <summary>HR or Higher committed the disputed adjustment after escalation.</summary>
    ResolvedAccepted,

    /// <summary>HR or Higher rejected the disputed adjustment after escalation.</summary>
    ResolvedRejected,
}
