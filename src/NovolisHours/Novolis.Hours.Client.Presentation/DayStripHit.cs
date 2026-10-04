namespace Novolis.Hours.Client.Presentation;

/// <summary>Hit-test result on the 06:00–20:00 axis.</summary>
public readonly record struct DayStripHit(DayStripHitKind Kind, int IntervalIndex, TimeOnly Time);
