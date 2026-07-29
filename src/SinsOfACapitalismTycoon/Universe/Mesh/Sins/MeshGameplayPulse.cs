using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

using SinsOfACapitalismTycoon.Universe.Mesh.Kernel;
namespace SinsOfACapitalismTycoon.Universe.Mesh.Sins;

/// <summary>
/// Daily gameplay traffic on the mesh: spot digests + retractions, escrow, Emergency.
/// </summary>
internal static class MeshGameplayPulse
{
  private static readonly HashSet<string> EmergencyKinds = new(StringComparer.OrdinalIgnoreCase)
  {
    "stockout",
    "soft-fail",
    "fuel-famine",
    "shock",
    "burnout",
    "grounding",
  };

  public static MeshState TickDay(
    MeshState mesh,
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    MilestoneLog milestones)
  {
    ArgumentNullException.ThrowIfNull(mesh);
    ArgumentNullException.ThrowIfNull(sim);
    ArgumentNullException.ThrowIfNull(ids);
    ArgumentNullException.ThrowIfNull(milestones);

    mesh = SyncSpotBoard(mesh, sim, ids);
    mesh = DrainEscrowNotices(mesh, sim, ids);
    mesh = PublishEmergencyFromMilestones(mesh, milestones, sim.State.Clock.Date.DayIndex);
    mesh = LaunchEngine.LaunchPending(mesh);
    return mesh;
  }

  /// <summary>
  /// Retract gone/changed offers, then publish fresh digests from each origin.
  /// Price/qty change ⇒ new logical key; old key is retracted so distant boards drop the stale line.
  /// </summary>
  public static MeshState SyncSpotBoard(
    MeshState mesh,
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    int takePerOrigin = 8)
  {
    var all = CaptainJobBoard.ListLiveFreight(sim, ids, TransitProfile.StandardCommercial, "sol", take: 64)
      .Select(s => s with { LogicalKey = SpotJobKeys.ForOffer(s) })
      .ToList();

    var liveKeys = all.Select(s => s.LogicalKey).ToHashSet(StringComparer.Ordinal);
    foreach (var (logicalKey, originId) in ids.SpotMesh.GoneRelativeTo(liveKeys))
    {
      if (!mesh.Nodes.ContainsKey(originId))
      {
        ids.SpotMesh.Forget(logicalKey);
        continue;
      }

      (mesh, _) = PublishEngine.PublishRetraction(
        mesh,
        MeshNodeId.From(originId),
        logicalKey,
        subject: $"Job gone · {logicalKey}");
      ids.SpotMesh.Forget(logicalKey);
    }

    foreach (var group in all.GroupBy(s => s.OriginSystemId, StringComparer.OrdinalIgnoreCase))
    {
      if (!mesh.Nodes.ContainsKey(group.Key))
      {
        continue;
      }

      var lines = group.OrderByDescending(s => s.Margin).Take(takePerOrigin).ToList();
      if (lines.Count == 0)
      {
        continue;
      }

      var body = SpotDigestCodec.FormatBody(lines);
      var origin = MeshNodeId.From(group.Key);
      (mesh, _) = PublishEngine.PublishPulse(
        mesh,
        origin,
        MeshAddress.ToFeed(MeshFeedId.CommerceSpot),
        priority: 2,
        subject: $"Spot · {lines[0].OriginName}",
        body: body,
        topic: MeshTopics.SpotDigest);

      foreach (var line in lines)
      {
        ids.SpotMesh.Remember(line.LogicalKey, group.Key);
      }
    }

    return mesh;
  }

  /// <summary>Test helper: retract a single known offer from its origin node.</summary>
  public static MeshState RetractOffer(
    MeshState mesh,
    string originSystemId,
    string logicalKey,
    SpotMeshLedger? ledger = null)
  {
    (mesh, _) = PublishEngine.PublishRetraction(
      mesh,
      MeshNodeId.From(originSystemId),
      logicalKey);
    ledger?.Forget(logicalKey);
    return mesh;
  }

  public static MeshState DrainEscrowNotices(
    MeshState mesh,
    EconomySimulation sim,
    CampaignWorld.Ids ids)
  {
    foreach (var notice in ids.Escrow.DrainNotices())
    {
      var shipId = MeshIdentityIds.Ship(notice.CarrierRegistryName);
      var from = ResolvePublishNode(mesh, sim, ids, notice.CarrierFirmId);
      var subject = notice.Kind switch
      {
        "open" => $"Escrow open · {notice.CarrierRegistryName}",
        "release" => $"Escrow release · {notice.CarrierRegistryName}",
        "clawback" => $"Escrow clawback · {notice.CarrierRegistryName}",
        _ => $"Escrow · {notice.CarrierRegistryName}",
      };
      (mesh, _) = PublishEngine.PublishPulse(
        mesh,
        from,
        MeshAddress.ToIdentity(shipId),
        priority: 3,
        subject: subject,
        body: notice.Detail,
        topic: MeshTopics.Escrow);

      if (notice.CarrierFirmId.Equals(ids.Carrier))
      {
        (mesh, _) = PublishEngine.PublishPulse(
          mesh,
          from,
          MeshAddress.ToIdentity(MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId)),
          priority: 3,
          subject: subject,
          body: notice.Detail,
          topic: MeshTopics.Escrow);
      }
    }

    return mesh;
  }

  public static MeshState PublishEmergencyFromMilestones(
    MeshState mesh,
    MilestoneLog milestones,
    int day)
  {
    if (!mesh.Nodes.ContainsKey("sol"))
    {
      return mesh;
    }

    var sol = MeshNodeId.From("sol");
    foreach (var e in milestones.Entries.Where(x => x.Day == day))
    {
      if (!EmergencyKinds.Contains(e.Kind))
      {
        continue;
      }

      var key = $"mesh-em:{e.Kind}|{e.Detail}";
      if (!milestones.TryClaimMeshPublish(key))
      {
        continue;
      }

      (mesh, _) = PublishEngine.PublishPulse(
        mesh,
        sol,
        MeshAddress.ToFeed(MeshFeedId.Emergency),
        priority: 10,
        subject: $"Alert · {e.Kind}",
        body: e.Detail,
        topic: MeshTopics.Emergency);
    }

    return mesh;
  }

  private static MeshNodeId ResolvePublishNode(
    MeshState mesh,
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    Novolis.Economy.FirmId firm)
  {
    var system = MeshMailboxSync.ResolveFirmSystemId(sim, ids, firm) ?? "sol";
    if (!mesh.Nodes.ContainsKey(system))
    {
      return MeshNodeId.From("sol");
    }

    return MeshNodeId.From(system);
  }
}
