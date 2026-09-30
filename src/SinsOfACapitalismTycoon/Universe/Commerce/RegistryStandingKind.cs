using Novolis.Economy;

namespace SinsOfACapitalismTycoon.Universe;

/// <summary>Coarse standing shared by every registry door.</summary>
internal enum RegistryStandingKind : byte
{
  Operable = 0,
  Restricted = 1,
  Suspended = 2,
  Revoked = 3,
}
