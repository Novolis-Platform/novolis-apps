using Novolis.Economy.Core;
using SinsOfACapitalismTycoon.Sim;

namespace SinsOfACapitalismTycoon.Sim.Policies;

/// <summary>Mutable counters policies can bump for drama reporting.</summary>
internal sealed class PolicyCounters
{
    public int TransfersStarted { get; set; }
    public int CreditDraws { get; set; }
    public int ShocksInjected { get; set; }
    public int CapacityExpansions { get; set; }
    public decimal MoneyCreatedByPolicy { get; set; }
}
