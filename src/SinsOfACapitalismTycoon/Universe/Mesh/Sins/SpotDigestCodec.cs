using System.Globalization;
using Novolis.Economy.Logistics;

using Novolis.Simulation.Mesh;
namespace SinsOfACapitalismTycoon.Universe.Mesh.Sins;

/// <summary>Compact line codec for <see cref="MeshTopics.SpotDigest"/> bodies.</summary>
internal static class SpotDigestCodec
{
  public const char FieldSep = '|';

  /// <summary>
  /// Line: logicalKey|origin|dest|sku|qty|lift|destBid|margin|profile|originName|destName
  /// </summary>
  public static string FormatLine(CaptainJobBoard.SpotCandidate c)
  {
    var key = SpotJobKeys.ForOffer(c);
    return string.Join(
      FieldSep,
      key,
      c.OriginSystemId,
      c.DestSystemId,
      c.SkuLabel,
      c.Quantity.ToString("0.###", CultureInfo.InvariantCulture),
      c.LiftLimit.ToString("0.###", CultureInfo.InvariantCulture),
      c.DestBid.ToString("0.###", CultureInfo.InvariantCulture),
      c.Margin.ToString("0.###", CultureInfo.InvariantCulture),
      c.Profile.ToString(),
      Sanitize(c.OriginName),
      Sanitize(c.DestName));
  }

  public static string FormatBody(IEnumerable<CaptainJobBoard.SpotCandidate> lines) =>
    string.Join('\n', lines.Select(FormatLine));

  public static IReadOnlyList<CaptainJobBoard.SpotCandidate> ParseBody(
    string body,
    string currentSystemId)
  {
    var list = new List<CaptainJobBoard.SpotCandidate>();
    if (string.IsNullOrWhiteSpace(body))
    {
      return list;
    }

    foreach (var raw in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      if (TryParseLine(raw, currentSystemId, out var spot))
      {
        list.Add(spot);
      }
    }

    return list;
  }

  public static bool TryParseLine(string line, string currentSystemId, out CaptainJobBoard.SpotCandidate spot)
  {
    spot = null!;
    var parts = line.Split(FieldSep);
    // New format (11+): key|origin|…  Legacy (10): origin|dest|…
    string logicalKey;
    string origin;
    string dest;
    string sku;
    int o;
    if (parts.Length >= 11 && parts[0].StartsWith("spot:", StringComparison.Ordinal))
    {
      logicalKey = parts[0];
      origin = parts[1];
      dest = parts[2];
      sku = parts[3];
      o = 4;
    }
    else if (parts.Length >= 10)
    {
      origin = parts[0];
      dest = parts[1];
      sku = parts[2];
      o = 3;
      logicalKey = "";
    }
    else
    {
      return false;
    }

    if (!decimal.TryParse(parts[o], NumberStyles.Number, CultureInfo.InvariantCulture, out var qty)
        || !decimal.TryParse(parts[o + 1], NumberStyles.Number, CultureInfo.InvariantCulture, out var lift)
        || !decimal.TryParse(parts[o + 2], NumberStyles.Number, CultureInfo.InvariantCulture, out var destBid)
        || !decimal.TryParse(parts[o + 3], NumberStyles.Number, CultureInfo.InvariantCulture, out var margin)
        || !Enum.TryParse<TransitProfile>(parts[o + 4], ignoreCase: true, out var profile))
    {
      return false;
    }

    var originName = Unsanitize(parts[o + 5]);
    var destName = Unsanitize(parts[o + 6]);
    if (string.IsNullOrEmpty(logicalKey))
    {
      logicalKey = SpotJobKeys.ForOffer(origin, dest, sku, qty, lift, destBid);
    }

    var atOrigin = origin.Equals(currentSystemId, StringComparison.OrdinalIgnoreCase);
    spot = new CaptainJobBoard.SpotCandidate(
      $"{sku} {originName}→{destName}",
      origin,
      dest,
      originName,
      destName,
      sku,
      qty,
      lift,
      destBid,
      margin,
      profile,
      $"Δ{margin:0.#} [{profile}] · mesh",
      atOrigin,
      atOrigin ? "AT DOCK" : "mesh",
      logicalKey);
    return true;
  }

  private static string Sanitize(string s) =>
    (s ?? string.Empty).Replace('|', '/').Replace('\n', ' ').Trim();

  private static string Unsanitize(string s) => s ?? string.Empty;
}
