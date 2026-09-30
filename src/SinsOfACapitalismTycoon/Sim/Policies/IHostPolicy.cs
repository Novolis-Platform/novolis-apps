using Novolis.Economy.Core;
using SinsOfACapitalismTycoon.Sim;

namespace SinsOfACapitalismTycoon.Sim.Policies;

internal interface IHostPolicy
{
    EconomyState ApplyIntents(EconomyState state, SeedIds ids, int periodIndex, int totalPeriods);
}
