using Novolis.Avalonia.StarMap;
using Novolis.Economy.Logistics;
using Novolis.Economy.Simulation;
using SinsOfACapitalismTycoon.Universe.Mesh.Sins;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>World-map pose for Calypso (docked or mid-leg).</summary>
internal static class ShipMapPose
{
  public static (double X, double Y, bool Visible) Compute(
    CampaignWorld.Ids ids,
    EconomyWorld world,
    string currentSystemId,
    ActiveShipment? ship,
    IReadOnlyList<StarMapPoint> points)
  {
    var byId = points.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
    if (ship is null || ship.IsLegacy)
    {
      if (byId.TryGetValue(currentSystemId, out var dock))
      {
        return (dock.X, dock.Y, true);
      }

      return (0, 0, false);
    }

    // At hub (loading / unloading / waiting): sit on CurrentHubId.
    if (ship.Phase is not ShipmentPhase.Underway
        || ship.Itinerary.CorridorIds.IsDefaultOrEmpty
        || ship.LegIndex < 0
        || ship.LegIndex >= ship.Itinerary.LegCount)
    {
      var hubSys = MeshMailboxSync.HubToSystemId(ids, ship.CurrentHubId) ?? currentSystemId;
      if (byId.TryGetValue(hubSys, out var atHub))
      {
        return (atHub.X, atHub.Y, true);
      }

      return (0, 0, false);
    }

    var corridorId = ship.Itinerary.CorridorIds[ship.LegIndex];
    if (!world.Corridors.TryGetValue(corridorId, out var corridor))
    {
      corridor = ids.Bridge.Corridors.FirstOrDefault(c => c.Id.Equals(corridorId));
    }

    if (corridor is null)
    {
      var hubSys = MeshMailboxSync.HubToSystemId(ids, ship.CurrentHubId) ?? currentSystemId;
      if (byId.TryGetValue(hubSys, out var fallback))
      {
        return (fallback.X, fallback.Y, true);
      }

      return (0, 0, false);
    }

    var fromSys = MeshMailboxSync.HubToSystemId(ids, corridor.From);
    var toSys = MeshMailboxSync.HubToSystemId(ids, corridor.To);
    if (fromSys is null || toSys is null
        || !byId.TryGetValue(fromSys, out var from)
        || !byId.TryGetValue(toSys, out var to))
    {
      return (0, 0, false);
    }

    // Progress along leg: 0 at depart hub → 1 at arrival.
    var total = Math.Max(1L, ship.LegHoursTotal);
    var remaining = Math.Clamp(ship.SegmentHoursRemaining, 0, total);
    var t = 1.0 - (remaining / (double)total);
    t = Math.Clamp(t, 0.0, 1.0);
    return (from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t, true);
  }
}
