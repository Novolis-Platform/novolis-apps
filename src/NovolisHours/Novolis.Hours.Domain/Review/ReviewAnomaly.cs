namespace Novolis.Hours.Domain.Review;

/// <summary>Transparent review concern that does not block later actions or registrations.</summary>
public sealed record ReviewAnomaly(
    string Code,
    string Message,
    string StageId,
    DateOnly DueDate);
