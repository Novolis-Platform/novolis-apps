namespace Novolis.Hours.Domain;

/// <summary>Explains why a balanced duration posting was created.</summary>
public enum HoursPostingReason
{
    /// <summary>Actual ordinary presence differed from expected time.</summary>
    RecordedFlexDifference,

    /// <summary>An employee-approved adjustment changed the flex saldo.</summary>
    EmployeeApprovedAdjustment,

    /// <summary>Unused positive flex was normalized under a configured carry policy; it is not a payment.</summary>
    FlexNormalization,
}
