using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Legal-person standing (Mining, Industry, Station, …).</summary>
internal sealed class FirmRegistryEntry : RegistryRecord
{
  public required FirmId FirmId { get; init; }

  public bool Blacklisted
  {
    get => Revoked;
    set => Revoked = value;
  }

  public override string StandingLabel =>
    Blacklisted ? "blacklisted"
    : Suspended ? "suspended"
    : LienPrincipal > 0m ? "encumbered"
    : "solvent";

  public static FirmRegistryEntry Create(FirmId firm, string registryName) =>
    new()
    {
      SubjectId = firm.Value,
      Kind = RegistryKind.Firm,
      RegistryName = registryName,
      FirmId = firm,
    };
}
