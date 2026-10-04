namespace Novolis.Hours.Domain.Work;

/// <summary>Origin of a work assertion, independent of whether it is later accepted.</summary>
public enum WorkRecordSource
{
    /// <summary>The employee recorded the assertion.</summary>
    Employee,

    /// <summary>An employer representative recorded the assertion.</summary>
    Employer,

    /// <summary>An external integration supplied the assertion.</summary>
    Integration,

    /// <summary>A system process generated the assertion.</summary>
    System,
}
