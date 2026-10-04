namespace Novolis.Hours.Domain.Review;

/// <summary>Projected outcome of one configured review stage.</summary>
public sealed record ReviewStageResult(
    ReviewStage Stage,
    DateOnly DueDate,
    bool IsComplete,
    ReviewAction? SatisfiedBy,
    bool IsOverdue);
