using Novolis.Economy.Core;
using SinsOfACapitalismTycoon.Sim;

namespace SinsOfACapitalismTycoon.Sim.Policies;

/// <summary>Inject a production-loss event at mid-horizon (periodIndex == total/2).</summary>
internal sealed class ShockPolicy(PolicyCounters counters, Money grossLoss) : IHostPolicy
{
    public EconomyState ApplyIntents(EconomyState state, SeedIds ids, int periodIndex, int totalPeriods)
    {
        var trigger = Math.Max(1, totalPeriods / 2);
        // periodIndex is 0-based before Advance; fire once when about to enter that period
        if (periodIndex + 1 != trigger || counters.ShocksInjected > 0)
            return state;

        var losses = new List<LossEvent>(state.PendingLosses)
        {
            new(ids.FactoryFirm, RiskKind.ProductionLoss, grossLoss)
        };
        counters.ShocksInjected++;
        return state with { PendingLosses = losses };
    }
}
