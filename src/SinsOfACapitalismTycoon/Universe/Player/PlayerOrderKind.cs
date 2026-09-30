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
