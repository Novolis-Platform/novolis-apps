namespace Novolis.Hours.Domain.Ledger;

/// <summary>Rebuilds employee saldo solely by replaying immutable postings.</summary>
public sealed class LedgerSaldoProjector
{
    /// <summary>Returns the EmployeeFlex balance represented by transactions.</summary>
    public TimeSpan Replay(IEnumerable<LedgerTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        return transactions
            .SelectMany(transaction => transaction.Postings)
            .Where(posting => posting.Account == DurationAccount.EmployeeFlex)
            .Aggregate(
                TimeSpan.Zero,
                (total, posting) => total + posting.SignedDuration);
    }
}
