using System.Collections.Immutable;

namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Build a tiny mesh for unit tests (no Astro dependency).</summary>
public static class MeshTestGraph
{
  public static readonly MeshNodeId Sol = MeshNodeId.From("sol");
  public static readonly MeshNodeId Wolf = MeshNodeId.From("wolf359");
  public static readonly MeshNodeId Proxima = MeshNodeId.From("proxima");
  public static readonly MeshNodeId Other = MeshNodeId.From("other");

  public static MeshState Triangle(MeshPolicy? policy = null, int bandwidth = 8)
  {
    var p = policy ?? new MeshPolicy(LossEveryNth: 0);
    var nodes = ImmutableDictionary<string, MeshNode>.Empty
      .Add(Sol.Value, new MeshNode(Sol, Sol.Value, "Sol", bandwidth))
      .Add(Wolf.Value, new MeshNode(Wolf, Wolf.Value, "Wolf 359", bandwidth))
      .Add(Proxima.Value, new MeshNode(Proxima, Proxima.Value, "Proxima", bandwidth))
      .Add(Other.Value, new MeshNode(Other, Other.Value, "Other", bandwidth));

    var edges = ImmutableArray.CreateBuilder<MeshEdge>();
    void Bidirectional(MeshNodeId a, MeshNodeId b, int pulseHours, int bulkHours = 20)
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
      Nodes = nodes,
      Edges = edges.ToImmutable(),
    };
  }
}
