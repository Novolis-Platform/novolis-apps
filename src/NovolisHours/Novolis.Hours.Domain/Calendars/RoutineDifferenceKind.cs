namespace Novolis.Hours.Domain.Calendars;

/// <summary>Non-blocking comparison category between observed and routine time.</summary>
public enum RoutineDifferenceKind
{
    /// <summary>Observed work fell outside the configured routine.</summary>
    OutsideRoutine,

    /// <summary>Configured routine time was not observed.</summary>
    MissingRoutine,

    /// <summary>Observed work was split into multiple intervals.</summary>
    SplitWorkDay,

    /// <summary>Observed work began later than the routine.</summary>
    LateStart,

    /// <summary>Observed work ended earlier than the routine.</summary>
    EarlyFinish,
}
