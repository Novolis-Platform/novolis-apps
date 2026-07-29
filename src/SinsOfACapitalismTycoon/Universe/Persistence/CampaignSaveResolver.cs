namespace SinsOfACapitalismTycoon.Universe;

internal static class CampaignSaveResolver
{
  public static async ValueTask<CampaignSaveRecord?> ResolveAsync(string? key, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(key))
    {
      return null;
    }

    var store = CampaignSaveStore.Default;
    if (key.Equals("latest", StringComparison.OrdinalIgnoreCase)
        || key.Equals("last", StringComparison.OrdinalIgnoreCase))
    {
      return store.TryGetLatest();
    }

    if (Guid.TryParse(key, out var id))
    {
      return await store.TryGetAsync(id, ct).ConfigureAwait(false);
    }

    return store.List().FirstOrDefault(s =>
      s.Label.Contains(key, StringComparison.OrdinalIgnoreCase)
      || s.Id.ToString("N").StartsWith(key, StringComparison.OrdinalIgnoreCase));
  }
}
