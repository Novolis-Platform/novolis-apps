using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>First escrow payday unlocks the Mesh job-board filter.</summary>
internal static class MeshBoardUnlock
{
  public static bool HasPlayerPayday(MilestoneLog milestones)
  {
    var hull = CampaignWorld.PlayerHullName;
    foreach (var e in milestones.Entries)
    {
      if (!e.Kind.Equals("escrow", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      if (!e.Detail.StartsWith("release", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      if (e.Detail.Contains(hull, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }

  /// <summary>Apply unlock from milestones (load games / day pulse). Forces dock board until unlocked.</summary>
  public static void Sync(PlayerControlState player, MilestoneLog milestones)
  {
    if (!player.MeshBoardUnlocked && HasPlayerPayday(milestones))
    {
      player.MeshBoardUnlocked = true;
    }

    if (!player.MeshBoardUnlocked)
    {
      player.DockBoardOnly = true;
    }
  }

  public static void NoteRelease(PlayerControlState? player, FirmId carrier, FirmId playerCarrier)
  {
    if (player is null || player.MeshBoardUnlocked)
    {
      return;
    }

    if (carrier.Equals(playerCarrier))
    {
      player.MeshBoardUnlocked = true;
    }
  }
}
