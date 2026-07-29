using SinsOfACapitalismTycoon.Universe.Mesh.Kernel;
namespace SinsOfACapitalismTycoon.Universe.Mesh.Sins;

/// <summary>Tracks which spot logical keys were last advertised on the mesh (for retraction).</summary>
internal sealed class SpotMeshLedger
{
  private readonly Dictionary<string, string> _keyToOrigin = new(StringComparer.Ordinal);

  public IReadOnlyCollection<string> AdvertisedKeys => _keyToOrigin.Keys;

  public void Remember(string logicalKey, string originSystemId) =>
    _keyToOrigin[logicalKey] = originSystemId;

  public bool TryGetOrigin(string logicalKey, out string originSystemId) =>
    _keyToOrigin.TryGetValue(logicalKey, out originSystemId!);

  public void Forget(string logicalKey) => _keyToOrigin.Remove(logicalKey);

  public IReadOnlyList<(string LogicalKey, string OriginSystemId)> GoneRelativeTo(
    IReadOnlySet<string> liveKeys)
  {
    var gone = new List<(string, string)>();
    foreach (var (key, origin) in _keyToOrigin)
    {
      if (!liveKeys.Contains(key))
      {
        gone.Add((key, origin));
      }
    }

    return gone;
  }
}
