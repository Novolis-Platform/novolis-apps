using Novolis.Economy.Core;
using SinsOfACapitalismTycoon.Sim;

namespace SinsOfACapitalismTycoon.Sim.Policies;

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
