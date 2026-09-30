using Novolis.Economy.Core;
using SinsOfACapitalismTycoon.Sim.Policies;

namespace SinsOfACapitalismTycoon.Sim;

internal sealed record ScenarioPack(
    ScenarioKind Kind,
    SeedKnobs Knobs,
    Func<PolicyCounters, SeedKnobs, IHostPolicy> CreatePolicy);
