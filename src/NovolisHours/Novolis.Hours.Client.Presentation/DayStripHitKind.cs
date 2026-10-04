namespace Novolis.Hours.Client.Presentation;

/// <summary>What a pointer position means on the actual-work lane.</summary>
public enum DayStripHitKind
{
    /// <summary>Empty lane — a new interval may begin.</summary>
    Empty,

    /// <summary>Interior of an existing actual interval.</summary>
    Body,

    /// <summary>Leading resize handle.</summary>
    StartHandle,

    /// <summary>Trailing resize handle.</summary>
    EndHandle,
}
