using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Seed-deterministic onboarding beats + soft-fail when grounded ≥7 days.</summary>
internal sealed class PlayerTutorialHost
{
  private readonly CampaignWorld.Ids _ids;
  private readonly MilestoneLog _milestones;
  private readonly PlayerControlState _player;
  private readonly HashSet<int> _beats = [];

  public PlayerTutorialHost(
    CampaignWorld.Ids ids,
    MilestoneLog milestones,
    PlayerControlState player)
  {
    _ids = ids;
    _milestones = milestones;
    _player = player;
  }

  public void TickDayEnd(EconomySimulation sim)
  {
    if (!_player.Enabled)
    {
      return;
    }

    var day = sim.State.Clock.Date.DayIndex;
    var firm = _ids.Carrier;

    // After the first 24h pulse, clock is typically day 1 — fire opening beat once.
    if (day <= 1 && _beats.Add(0))
    {
      _milestones.Add(day, "tutorial",
        $"{CampaignWorld.PlayerMasterLabel} registered — Marsh check / CCA desk ({CampaignWorld.PlayerFlavorId})");
    }

    if (day is >= 2 and <= 3 && _beats.Add(2))
    {
      _milestones.Add(day, "tutorial",
        "First escrowed short charter suggested — Industrial/Mining under 8 ly");
    }

    if (!_ids.Registry.CanOperate(firm))
    {
      _player.SoftFailGroundedDays++;
    }
    else
    {
      _player.SoftFailGroundedDays = 0;
    }

    if (_player.SoftFailGroundedDays >= 7 && !_player.SoftFailRaised)
    {
      _player.SoftFailRaised = true;
      _milestones.Add(day, "soft-fail",
        $"{CampaignWorld.PlayerHullName} grounded {_player.SoftFailGroundedDays}d — registry hold / cash death risk");
    }
  }
}
