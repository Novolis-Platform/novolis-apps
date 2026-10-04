namespace Novolis.Hours.Domain.Review;

/// <summary>Projected human destination for an unresolved dispute.</summary>
public sealed record ReviewEscalation(
    Guid SourceActionId,
    ReviewEscalationTarget Target,
    string Reason);
