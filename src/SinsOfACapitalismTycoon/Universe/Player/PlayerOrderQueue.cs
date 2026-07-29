using Novolis.Economy;
using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

internal enum PlayerOrderKind
{
  /// <summary>Commit a spot lot into the dock manifest (must be docked at origin).</summary>
  CommitSpot,
  /// <summary>Depart with staged manifest SKU (PlanShipment).</summary>
  DepartManifest,
  /// <summary>Empty-hull travel to a system.</summary>
  TravelTo,
  SetDefaultProfile,
  PayPremium,
  RequestOverhaul,
  AcceptStandby,
  RefuseStandby,
  Wait,
  /// <summary>Buy from a dock ASK into Calypso inventory (TransferGoodsForCash).</summary>
  MarketBuy,
  /// <summary>Sell Calypso dock inventory into a BID (TransferGoodsForCash).</summary>
  MarketSell,
  /// <summary>Legacy alias — prefer CommitSpot.</summary>
  AcceptHaul = CommitSpot,
}

/// <summary>One captain intent for ST Calypso (James).</summary>
internal sealed record PlayerOrder(
  PlayerOrderKind Kind,
  string? OriginSystemId = null,
  string? DestSystemId = null,
  string? SkuLabel = null,
  decimal Quantity = 0m,
  decimal LiftLimit = 0m,
  decimal DestBid = 0m,
  TransitProfile Profile = TransitProfile.StandardCommercial,
  Guid? CounterpartyFirmId = null);

/// <summary>Thread-safe queue of player intents drained by <see cref="PlayerTrampAgent"/>.</summary>
internal sealed class PlayerOrderQueue
{
  private readonly object _gate = new();
  private readonly Queue<PlayerOrder> _q = new();

  public int Count
  {
    get
    {
      lock (_gate)
      {
        return _q.Count;
      }
    }
  }

  public void Enqueue(PlayerOrder order)
  {
    lock (_gate)
    {
      _q.Enqueue(order);
    }
  }

  public bool TryDequeue(out PlayerOrder order)
  {
    lock (_gate)
    {
      if (_q.Count == 0)
      {
        order = null!;
        return false;
      }

      order = _q.Dequeue();
      return true;
    }
  }

  public void Clear()
  {
    lock (_gate)
    {
      _q.Clear();
    }
  }
}

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
