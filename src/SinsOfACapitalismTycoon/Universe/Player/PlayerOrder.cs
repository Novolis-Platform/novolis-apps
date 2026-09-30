using Novolis.Economy;
using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

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
