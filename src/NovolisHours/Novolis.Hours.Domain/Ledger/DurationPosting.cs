namespace Novolis.Hours.Domain.Ledger;

/// <summary>One immutable signed duration movement on a ledger account.</summary>
public sealed record DurationPosting
{
    /// <summary>Initializes a posting.</summary>
    public DurationPosting(DurationAccount account, TimeSpan signedDuration)
    {
        Account = account;
        SignedDuration = signedDuration;
    }

    /// <summary>Account receiving the movement.</summary>
    public DurationAccount Account { get; }

    /// <summary>Signed duration; positive and negative are both meaningful.</summary>
    public TimeSpan SignedDuration { get; }
}
