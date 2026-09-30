using Novolis.Economy.Core;
using Novolis.Economy.Core.Extensions;

namespace SinsOfACapitalismTycoon.Sim;

/// <summary>Horizon-cumulative Core period flows (summed each Advance).</summary>
internal sealed record CumulativeFlowStats(
    decimal MoneyCreated,
    decimal MoneyDestroyed,
    decimal WagesAccrued,
    decimal TaxCollected,
    decimal TransfersPaid,
    decimal ObligationsPaid,
    decimal ProductionOutputValue,
    decimal LastPeriodNetMoneyCreated);
