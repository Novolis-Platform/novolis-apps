namespace Novolis.Hours.Domain;

/// <summary>Stable event names for the append-only worktime journal.</summary>
public static class HoursEventType
{
    /// <summary>A presence fact was registered.</summary>
    public const string WorkRegistered = "work.registered.v1";

    /// <summary>An adjustment awaiting employee response was proposed.</summary>
    public const string AdjustmentProposed = "adjustment.proposed.v1";

    /// <summary>An employee or reviewer changed the adjustment state.</summary>
    public const string AdjustmentUpdated = "adjustment.updated.v1";

    /// <summary>A monthly approval period was opened.</summary>
    public const string ApprovalPeriodOpened = "approval-period.opened.v1";

    /// <summary>A monthly approval period was updated or assessed for overdue deadlines.</summary>
    public const string ApprovalPeriodUpdated = "approval-period.updated.v1";
}
