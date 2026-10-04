namespace Novolis.Hours.Client.Presentation;

/// <summary>Active pointer gesture on the day strip.</summary>
public enum DayStripDragKind
{
    /// <summary>No gesture.</summary>
    None,

    /// <summary>Move an existing actual interval.</summary>
    Move,

    /// <summary>Resize the start of an actual interval.</summary>
    ResizeStart,

    /// <summary>Resize the end of an actual interval.</summary>
    ResizeEnd,

    /// <summary>Create a new actual interval.</summary>
    Create,

    /// <summary>Paint a Dimension stroke clipped to actual work.</summary>
    Paint,
}
