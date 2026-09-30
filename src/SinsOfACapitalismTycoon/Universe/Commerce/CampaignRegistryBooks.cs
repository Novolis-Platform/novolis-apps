using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>
/// Campaign books: ship / firm / license books under one CCA-shaped roof.
/// Spectre and pulses still talk mostly to <see cref="Ships"/>; firms and licenses are first-class doors.
/// </summary>
internal sealed class CampaignRegistryBooks
{
  public ShipRegistry Ships { get; } = new();

  public RegistryBook<FirmRegistryEntry> Firms { get; } = new(RegistryKind.Firm);

  public RegistryBook<LicenseRegistryEntry> Licenses { get; } = new(RegistryKind.License);

  /// <summary>Backward-compatible alias used across pulses and Spectre.</summary>
  public ShipRegistry Registry => Ships;

  public IEnumerable<RegistryRecord> AllRecords()
  {
    foreach (var s in Ships.Entries)
    {
      yield return s;
    }

    foreach (var f in Firms.Entries)
    {
      yield return f;
    }

    foreach (var l in Licenses.Entries)
    {
      yield return l;
    }
  }

  public bool CanAct(RegistryKind kind, Guid subjectId) =>
    kind switch
    {
      RegistryKind.Ship => Ships.CanOperate(FirmId.From(subjectId)),
      RegistryKind.Firm => Firms.CanAct(subjectId),
      RegistryKind.License => Licenses.CanAct(subjectId),
      _ => false,
    };
}
