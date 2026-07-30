using Novolis.Economy;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;

using Novolis.Simulation.Mesh;
namespace SinsOfACapitalismTycoon.Universe.Mesh.Sins;

/// <summary>
/// Keeps ship + player person mailboxes co-located with their current star-system node.
/// Delays only matter once identities move with the hull.
/// While a hull is in FTL (<see cref="ShipmentPhase.Underway"/>), its mesh link is down —
/// no mailbox move, pull, or push until the ship is in-system again.
/// </summary>
internal static class MeshMailboxSync
{
  /// <summary>True while the shipment is on a corridor between hubs (FTL / underway leg).</summary>
  public static bool IsInFtl(ActiveShipment? ship) =>
    ship is { Status: ShipmentStatus.InTransit, Phase: ShipmentPhase.Underway };

  public static MeshState SyncHour(
    MeshState mesh,
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    string playerSystemId)
  {
    ArgumentNullException.ThrowIfNull(mesh);
    ArgumentNullException.ThrowIfNull(sim);
    ArgumentNullException.ThrowIfNull(ids);

    var playerInFtl = false;
    foreach (var entry in ids.Registry.Entries)
    {
      var ship = sim.State.World.Shipments.FirstOrDefault(s =>
        !s.IsLegacy && s.FirmId.Equals(entry.FirmId) && s.Status == ShipmentStatus.InTransit);
      var inFtl = IsInFtl(ship);
      if (entry.FirmId.Equals(ids.Carrier))
      {
        playerInFtl = inFtl;
      }

      var shipId = MeshIdentityIds.Ship(entry.RegistryName);
      if (inFtl)
      {
        mesh = SetLinked(mesh, shipId, linked: false);
        continue;
      }

      string? systemId;
      if (entry.FirmId.Equals(ids.Carrier) && !string.IsNullOrEmpty(playerSystemId))
      {
        systemId = playerSystemId;
      }
      else
      {
        systemId = ResolveFirmSystemId(sim, ids, entry.FirmId) ?? "sol";
      }

      if (!mesh.Nodes.ContainsKey(systemId))
      {
        continue;
      }

      // Link before Move so arrival catch-up (pull/push/Emergency) can run.
      mesh = SetLinked(mesh, shipId, linked: true);
      mesh = MailboxEngine.Move(
        mesh,
        shipId,
        MeshNodeId.From(systemId));
    }

    var personId = MeshIdentityIds.Person(CampaignWorld.PlayerFlavorId);
    if (playerInFtl)
    {
      mesh = SetLinked(mesh, personId, linked: false);
    }
    else
    {
      var personNode = string.IsNullOrEmpty(playerSystemId) || !mesh.Nodes.ContainsKey(playerSystemId)
        ? MeshNodeId.From("sol")
        : MeshNodeId.From(playerSystemId);
      mesh = SetLinked(mesh, personId, linked: true);
      mesh = MailboxEngine.Move(mesh, personId, personNode);
    }

    return mesh;
  }

  private static MeshState SetLinked(MeshState mesh, MeshIdentityId owner, bool linked)
  {
    if (!mesh.Mailboxes.TryGetValue(owner.Value, out var box))
    {
      return mesh;
    }

    if (box.LinkedToNode == linked)
    {
      return mesh;
    }

    return mesh with
    {
      Mailboxes = mesh.Mailboxes.SetItem(owner.Value, box with { LinkedToNode = linked }),
    };
  }

  /// <summary>Best-effort: active shipment hub, else firm home dock if known, else null.</summary>
  public static string? ResolveFirmSystemId(
    EconomySimulation sim,
    CampaignWorld.Ids ids,
    FirmId firm)
  {
    var ship = sim.State.World.Shipments.FirstOrDefault(s =>
      !s.IsLegacy && s.FirmId.Equals(firm) && s.Status == ShipmentStatus.InTransit);
    if (ship is not null)
    {
      return HubToSystemId(ids, ship.CurrentHubId);
    }

    var waiting = sim.State.World.Shipments.FirstOrDefault(s =>
      !s.IsLegacy && s.FirmId.Equals(firm));
    if (waiting is not null)
    {
      return HubToSystemId(ids, waiting.CurrentHubId);
    }

    if (firm.Equals(ids.Carrier))
    {
      foreach (var hub in ids.Bridge.Hubs)
      {
        if (hub.SystemId.Equals("sol", StringComparison.OrdinalIgnoreCase))
        {
          return hub.SystemId;
        }
      }
    }

    return null;
  }

  public static string? HubToSystemId(CampaignWorld.Ids ids, TransportHubId hubId)
  {
    foreach (var site in ids.Sites.Values)
    {
      if (site.Hub.HubId.Equals(hubId))
      {
        return site.Hub.SystemId;
      }
    }

    foreach (var hub in ids.Bridge.Hubs)
    {
      if (hub.HubId.Equals(hubId))
      {
        return hub.SystemId;
      }
    }

    return null;
  }
}
