namespace Novolis.Hours.Domain.Work;

/// <summary>Meaning of a work assertion at the time it was recorded.</summary>
public enum WorkRecordIntent
{
    /// <summary>The actor asserts that the configured routine represents the work.</summary>
    WorkedAsScheduled,

    /// <summary>The actor supplied observed work intervals.</summary>
    ManualRegistration,

    /// <summary>The actor supplied a new assertion correcting an earlier one.</summary>
    Correction,
}
