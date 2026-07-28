using Novolis.Economy.Core;
using SinsOfACapitalismTycoon.Sim;

namespace SinsOfACapitalismTycoon.Sim.Policies;

internal interface IHostPolicy
{
    EconomyState ApplyIntents(EconomyState state, SeedIds ids, int periodIndex, int totalPeriods);
}

internal sealed class CompositePolicy(params IHostPolicy[] policies) : IHostPolicy
{
    private readonly IHostPolicy[] _policies = policies;

    public EconomyState ApplyIntents(EconomyState state, SeedIds ids, int periodIndex, int totalPeriods)
    {
        foreach (var p in _policies)
            state = p.ApplyIntents(state, ids, periodIndex, totalPeriods);
        return state;
    }
}

/// <summary>Mutable counters policies can bump for drama reporting.</summary>
internal sealed class PolicyCounters
{
    public int TransfersStarted { get; set; }
    public int CreditDraws { get; set; }
    public int ShocksInjected { get; set; }
    public int CapacityExpansions { get; set; }
    public decimal MoneyCreatedByPolicy { get; set; }
}
