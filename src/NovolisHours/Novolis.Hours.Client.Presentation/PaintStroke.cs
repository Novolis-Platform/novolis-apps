namespace Novolis.Hours.Client.Presentation;

/// <summary>One Dimension stroke sitting on actual work.</summary>
public sealed record PaintStroke(
    string DimensionId,
    string ValueId,
    TimeOnly Start,
    TimeOnly End);
