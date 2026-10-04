using System.Collections.Immutable;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Ledger;

/// <summary>Immutable balanced duration transaction.</summary>
public sealed record LedgerTransaction
{
    /// <summary>Initializes and validates a balanced transaction.</summary>
    public LedgerTransaction(
        Guid id,
        WorkDayKey workDay,
        Guid sourceRegistrationId,
        ImmutableArray<DurationPosting> postings,
        string reason)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A ledger transaction requires an identity.", nameof(id));
        }

        if (sourceRegistrationId == Guid.Empty)
        {
            throw new ArgumentException(
                "A ledger transaction requires a source registration.",
                nameof(sourceRegistrationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (postings.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "A ledger transaction requires at least one posting.",
                nameof(postings));
        }

        var balance = postings.Aggregate(
            TimeSpan.Zero,
            (total, posting) => total + posting.SignedDuration);
        if (balance != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A duration ledger transaction must sum to zero.",
                nameof(postings));
        }

        Id = id;
        WorkDay = workDay;
        SourceRegistrationId = sourceRegistrationId;
        Postings = postings;
        Reason = reason.Trim();
    }

    /// <summary>Transaction identity.</summary>
    public Guid Id { get; }

    /// <summary>Logical workday that produced the transaction.</summary>
    public WorkDayKey WorkDay { get; }

    /// <summary>Registration identity that anchors the transaction.</summary>
    public Guid SourceRegistrationId { get; }

    /// <summary>Balanced immutable postings.</summary>
    public ImmutableArray<DurationPosting> Postings { get; }

    /// <summary>Neutral human-readable reason for the movement.</summary>
    public string Reason { get; }
}
