namespace Novolis.Hours.Domain;

/// <summary>Identifies the worktime role used for an auditable action.</summary>
public enum HoursActorRole
{
    /// <summary>Automated policy or deadline evaluation.</summary>
    System,

    /// <summary>The employee whose worktime is being recorded.</summary>
    Employee,

    /// <summary>The employee's direct manager.</summary>
    Manager,

    /// <summary>Human-resources reviewer.</summary>
    HumanResources,

    /// <summary>A designated higher-level reviewer.</summary>
    Higher,

    /// <summary>A read-only investigator for an organisation or configured scope.</summary>
    Auditor,

    /// <summary>A tenant administrator.</summary>
    Administrator,
}
