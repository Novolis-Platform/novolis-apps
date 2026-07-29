using Novolis.Astro.Catalog;
using Novolis.Astro.Catalog.Data;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Campaign star field from <see cref="CatalogPacks.NearSol100"/> (Astro Catalog.Data),
/// not a local JSON fork. Memoized — desk refresh must not rebuild every frame.
/// </summary>
internal static class SinsCatalog
{
  private static readonly Lazy<StarCatalog> NearSol = new(CreateNearSol, LazyThreadSafetyMode.ExecutionAndPublication);

  public static StarCatalog Load() => NearSol.Value;

  private static StarCatalog CreateNearSol()
  {
    var catalog = CatalogPacks.ToCatalog(CatalogPacks.NearSol100);
    if (!catalog.TryGet("sol", out _))
    {
      throw new InvalidOperationException("NearSol100 pack must include sol.");
    }

    return catalog;
  }
}
