using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Build a tiny mesh for unit tests (no Astro dependency).</summary>
public static class MeshTestGraph
{
  public static readonly MeshHubId Sol = MeshHubId.From("sol");
  public static readonly MeshHubId Wolf = MeshHubId.From("wolf359");
  public static readonly MeshHubId Proxima = MeshHubId.From("proxima");
  public static readonly MeshHubId Other = MeshHubId.From("other");

  public static MeshState Triangle(MeshPolicy? policy = null, int bandwidth = 8)
  {
    var p = policy ?? new MeshPolicy(LossEveryNth: 0);
    var hubs = ImmutableDictionary<string, MeshHub>.Empty
      .Add(Sol.Value, new MeshHub(Sol, Sol.Value, "Sol", bandwidth))
      .Add(Wolf.Value, new MeshHub(Wolf, Wolf.Value, "Wolf 359", bandwidth))
      .Add(Proxima.Value, new MeshHub(Proxima, Proxima.Value, "Proxima", bandwidth))
      .Add(Other.Value, new MeshHub(Other, Other.Value, "Other", bandwidth));

    // sol — wolf — proxima; other isolated from sol except via proxima? 
    // sol-wolf, wolf-proxima, sol-proxima (triangle); other only linked to proxima
    var edges = ImmutableArray.CreateBuilder<MeshEdge>();
    void Bidirectional(MeshHubId a, MeshHubId b, int pulseHours, int bulkHours = 20)
    {
      edges.Add(new MeshEdge(a, b, pulseHours, bulkHours, pulseHours));
      edges.Add(new MeshEdge(b, a, pulseHours, bulkHours, pulseHours));
    }

    Bidirectional(Sol, Wolf, pulseHours: 2);
    Bidirectional(Wolf, Proxima, pulseHours: 2);
    Bidirectional(Sol, Proxima, pulseHours: 3);
    Bidirectional(Proxima, Other, pulseHours: 2);

    return MeshState.Empty(p) with
    {
      Hubs = hubs,
      Edges = edges.ToImmutable(),
    };
  }
}
