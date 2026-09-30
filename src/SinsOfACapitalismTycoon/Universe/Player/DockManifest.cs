using Novolis.Economy;
using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Committed spot lots at the current load dock (capacity ≤ HullCargoCapacity).</summary>
internal sealed class DockManifest
{
  private readonly List<Lot> _lots = [];

  public sealed record Lot(
    string OriginSystemId,
    string DestSystemId,
    string SkuLabel,
    ProductId ProductId,
    decimal Quantity,
    decimal LiftLimit,
    decimal DestBid,
    TransitProfile Profile);

  public IReadOnlyList<Lot> Lots => _lots;

  public decimal Used => _lots.Sum(l => l.Quantity);

  public decimal Room => Math.Max(0m, CampaignWorld.HullCargoCapacity - Used);

  public void Clear() => _lots.Clear();

  public bool TryAdd(
    string originSystemId,
    string destSystemId,
    string skuLabel,
    ProductId productId,
    decimal quantity,
    decimal liftLimit,
    decimal destBid,
    TransitProfile profile,
    out string fail)
  {
    fail = "";
    if (quantity < 1m)
    {
      fail = "qty";
      return false;
    }

    if (_lots.Count > 0
        && !_lots[0].OriginSystemId.Equals(originSystemId, StringComparison.OrdinalIgnoreCase))
    {
      fail = "origin-mismatch";
      return false;
    }

    var room = Room;
    if (quantity > room + 0.0001m)
    {
      fail = "no-room";
      return false;
    }

    for (var i = 0; i < _lots.Count; i++)
    {
      var lot = _lots[i];
      if (lot.ProductId.Equals(productId)
          && lot.DestSystemId.Equals(destSystemId, StringComparison.OrdinalIgnoreCase)
          && lot.Profile == profile)
      {
        _lots[i] = lot with { Quantity = lot.Quantity + quantity };
        return true;
      }
    }

    _lots.Add(new Lot(
      originSystemId, destSystemId, skuLabel, productId, quantity, liftLimit, destBid, profile));
    return true;
  }

  public Lot? TakeForDepart(string? skuLabel)
  {
    if (_lots.Count == 0)
    {
      return null;
    }

    if (string.IsNullOrWhiteSpace(skuLabel))
    {
      var first = _lots[0];
      _lots.RemoveAt(0);
      return first;
    }

    var idx = _lots.FindIndex(l =>
      l.SkuLabel.Equals(skuLabel, StringComparison.OrdinalIgnoreCase));
    if (idx < 0)
    {
      return null;
    }

    var lot = _lots[idx];
    _lots.RemoveAt(idx);
    return lot;
  }
}
