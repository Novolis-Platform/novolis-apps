using Novolis.Economy;
using Novolis.Economy.Logistics;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Cargo time-value → transit profile (FTL ops brief: cargo determines the timetable).
/// Bulk ore → Slow; Capital/Final → Priority for owner-masters; mega-hauler stays Slow.
/// </summary>
internal static class TransitChooser
{
  public static TransitProfile ForTramp(ProductId product, CampaignWorld.Ids ids)
  {
    if (product.Equals(ids.Ore) || product.Equals(ids.Fuel))
    {
      return TransitProfile.SlowEconomic;
    }

    if (product.Equals(ids.Parts) || product.Equals(ids.Goods))
    {
      return TransitProfile.PriorityCommercial;
    }

    return TransitProfile.StandardCommercial;
  }

  public static TransitProfile ForMegaHauler(ProductId product, CampaignWorld.Ids ids)
  {
    _ = product;
    _ = ids;
    // Automated mega-haulers optimize cost and wear, not schedule.
    return TransitProfile.SlowEconomic;
  }
}
