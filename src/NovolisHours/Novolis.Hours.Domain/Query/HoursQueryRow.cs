namespace Novolis.Hours.Domain;

/// <summary>Uniform immutable row returned by the query engine for rendering, export, or API transport.</summary>
public sealed record HoursQueryRow(
    DateOnly? Day,
    string Category,
    string Description,
    TimeSpan? Duration = null,
    string? State = null,
    Guid? SourceId = null);
