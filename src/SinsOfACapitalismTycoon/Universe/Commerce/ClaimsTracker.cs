namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Per-session dedupe for claim edges — never process-global.</summary>
internal sealed class ClaimsTracker
{
  private readonly HashSet<Guid> _seenCancelled = [];
  private readonly HashSet<string> _seenBombEdge = new(StringComparer.Ordinal);

  public bool TryMarkCancelled(Guid shipmentId) => _seenCancelled.Add(shipmentId);

  public bool TryMarkBombEdge(string key) => _seenBombEdge.Add(key);
}
