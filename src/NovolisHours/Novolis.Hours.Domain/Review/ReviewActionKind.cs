namespace Novolis.Hours.Domain.Review;

/// <summary>Append-only action kinds used to project review state.</summary>
public enum ReviewActionKind
{
    /// <summary>Employee submitted the period for review.</summary>
    Submit,

    /// <summary>A responsible reviewer approved the configured stage.</summary>
    Approve,

    /// <summary>A person acknowledged a period or employer assertion.</summary>
    Acknowledge,

    /// <summary>An actor recorded disagreement without deleting history.</summary>
    Dispute,

    /// <summary>A responsible actor recorded a dispute resolution.</summary>
    Resolve,

    /// <summary>Neutral review context.</summary>
    Comment,
}
