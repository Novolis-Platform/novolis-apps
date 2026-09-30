using Novolis.Economy.Agents;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Berth / solvency autopilot policy — <see cref="PlayerTrampAgent"/> only executes orders.</summary>
internal interface IBerthAutopilotPolicy
{
  /// <summary>True when an order was queued this tick.</summary>
  bool Tick(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state);
}
