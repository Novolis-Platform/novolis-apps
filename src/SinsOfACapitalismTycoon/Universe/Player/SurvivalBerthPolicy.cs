using Novolis.Economy.Agents;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Rule SurvivalCaptain wrapped as a berth policy component.</summary>
internal sealed class SurvivalBerthPolicy : IBerthAutopilotPolicy
{
  public static SurvivalBerthPolicy Instance { get; } = new();

  public bool Tick(
    PlayerTrampAgent agent,
    AgentContext context,
    CampaignWorld.Ids ids,
    PlayerControlState state) =>
    SurvivalCaptain.Tick(agent, context, ids, state);
}
