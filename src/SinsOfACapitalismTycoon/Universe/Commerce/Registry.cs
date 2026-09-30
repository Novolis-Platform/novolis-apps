using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Which ledger this registry record belongs to (CCA-shaped OS).</summary>
internal enum RegistryKind : byte
{
  Ship = 0,
  Firm = 1,
  License = 2,
  Vehicle = 3,
}
