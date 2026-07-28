using Novolis.Economy.Core;
using SinsOfACapitalismTycoon.Sim;

namespace SinsOfACapitalismTycoon.Sim.Policies;

/// <summary>One-shot factory capacity expansion after credit has been drawn.</summary>
internal sealed class CapacityExpandPolicy(
    PolicyCounters counters,
    int afterPeriod,
    decimal newInstalledCapacity) : IHostPolicy
{
    public EconomyState ApplyIntents(EconomyState state, SeedIds ids, int periodIndex, int totalPeriods)
    {
        if (counters.CapacityExpansions > 0)
            return state;
        if (periodIndex + 1 < afterPeriod)
            return state;
        if (counters.CreditDraws < 1)
            return state;
        if (!state.Activities.TryGetValue(ids.FactoryActivityId, out var act))
            return state;

        var activities = new Dictionary<ActivityId, Activity>(state.Activities)
        {
            [ids.FactoryActivityId] = act with { InstalledCapacity = newInstalledCapacity }
        };
        counters.CapacityExpansions++;
        return state with { Activities = activities };
    }
}
