using Novolis.Economy.Finance;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Shared handles for one campaign hour/day pulse — steps stay dumb side-effect units.</summary>
internal sealed class CampaignPulseContext
{
  public required EconomySimulation Sim { get; init; }
  public required CampaignWorld.Ids Ids { get; init; }
  public required SinsAgents.Bundle Agents { get; init; }
  public required MilestoneLog Milestones { get; init; }
  public required ShipBiographyLog Biographies { get; init; }
  public required CreditCirculation Credits { get; init; }
  public required PlayerControlState Player { get; init; }
  public required ClaimsTracker Claims { get; init; }
  public required SimEventCursor Events { get; init; }
  public required CampaignNoticeBus Notices { get; init; }
  public required CampaignDramaHost Drama { get; init; }
  public PlayerTutorialHost? Tutorial { get; init; }
  public required Func<string> CurrentSystemId { get; init; }
  public required Action EvaluateLastTramp { get; init; }
  public bool MaxSpeedThroughput { get; init; }
}
