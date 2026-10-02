namespace PresenceLedger.Core;

/// <summary>One arrival, departure, or open stay on a local calendar day.</summary>
public readonly record struct AttendanceMark(
    DateOnly Day,
    DateTimeOffset At,
    int Order,
    Guid LocationId,
    string Place,
    string Line);
