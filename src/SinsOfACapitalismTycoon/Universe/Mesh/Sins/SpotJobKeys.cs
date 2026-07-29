using System.Globalization;

using SinsOfACapitalismTycoon.Universe.Mesh.Kernel;
namespace SinsOfACapitalismTycoon.Universe.Mesh.Sins;

/// <summary>Stable logical keys for spot offers (price/qty changes ⇒ new key; old must retract).</summary>
internal static class SpotJobKeys
{
  public static string ForOffer(
    string originSystemId,
    string destSystemId,
    string skuLabel,
    decimal quantity,
    decimal liftLimit,
    decimal destBid) =>
    string.Join(
      ':',
      "spot",
      originSystemId,
      destSystemId,
      skuLabel,
      quantity.ToString("0.###", CultureInfo.InvariantCulture),
      liftLimit.ToString("0.###", CultureInfo.InvariantCulture),
      destBid.ToString("0.###", CultureInfo.InvariantCulture));

  public static string ForOffer(CaptainJobBoard.SpotCandidate c) =>
    string.IsNullOrEmpty(c.LogicalKey)
      ? ForOffer(c.OriginSystemId, c.DestSystemId, c.SkuLabel, c.Quantity, c.LiftLimit, c.DestBid)
      : c.LogicalKey;
}
