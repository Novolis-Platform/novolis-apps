using Novolis.Astro.Assessment;
using Novolis.Economy;
using Novolis.Economy.Simulation;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Seed-time invariants for potential-gated campaign geography.</summary>
internal static class SeedInvariants
{
  public static void Assert(CampaignWorld.Ids ids, EconomySimulation sim)
  {
    var failures = new List<string>();

    failures.AddRange(SystemRoleInvariants.CollectFailures(
      ids.Bridge.Hubs.Select(h => (h.SystemId, h.Role, h.Profile.Potential))));

    // Map cohort area → hub via facility Area bindings created at seed.
    var areaToHub = new Dictionary<GeographicAreaId, AstroEconomyBridge.HubBinding>();
    foreach (var site in ids.Sites.Values)
    {
      foreach (var fac in sim.State.World.Facilities.Values)
      {
        if (fac.Area is { } area && fac.StorageLocation.Equals(site.Hub.LocationId))
        {
          areaToHub[area] = site.Hub;
        }
      }
    }

    foreach (var cohort in sim.State.World.Cohorts)
    {
      if (!areaToHub.TryGetValue(cohort.Definition.Area, out var hub))
      {
        failures.Add($"Cohort {cohort.Definition.Id.Value:N} area has no hub binding");
        continue;
      }

      if (hub.Profile.Potential.Agriculture == 0
          && hub.Role is not SystemRole.Mining)
      {
        failures.Add($"Cohort on barren system {hub.SystemId}");
      }
    }

    if (failures.Count > 0)
    {
      throw new InvalidOperationException(
        "Campaign seed invariants failed:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }
  }
}
