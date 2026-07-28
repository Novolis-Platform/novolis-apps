using Novolis.Astro.Catalog;
using Novolis.Astro.Catalog.Data;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Campaign star field from <see cref="CatalogPacks.NearSol100"/> (Astro Catalog.Data),
/// not a local JSON fork.
/// </summary>
internal static class SinsCatalog
{
  public static StarCatalog Load()
  {
    var catalog = CatalogPacks.ToCatalog(CatalogPacks.NearSol100);
    if (!catalog.TryGet("sol", out _))
    {
      throw new InvalidOperationException("NearSol100 pack must include sol.");
    }

    return catalog;
  }
}
