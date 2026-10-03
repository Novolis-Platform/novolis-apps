namespace Novolis.Hours.Domain;

/// <summary>Workflow state for a monthly worktime approval period.</summary>
public enum HoursApprovalState
{
    /// <summary>Worktime can be registered and the employee submission deadline is visible.</summary>
    Registered,

    /// <summary>The employee submitted their monthly record for review.</summary>
    EmployeeSubmitted,

    /// <summary>The manager reviewed the period.</summary>
    ManagerApproved,

    /// <summary>A disagreement was routed to HR or Higher without locking registration.</summary>
    Escalated,

    /// <summary>The escalated review has a recorded outcome.</summary>
    Resolved,
}
