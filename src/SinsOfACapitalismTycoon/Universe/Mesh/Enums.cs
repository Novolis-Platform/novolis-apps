namespace SinsOfACapitalismTycoon.Universe.Mesh.Kernel;

/// <summary>Traffic layer — pulse is sprint drones; bulk is freight-class; feed is public channel cargo.</summary>
internal enum MeshTrafficLayer
{
  Pulse = 0,
  Bulk = 1,
  Feed = 2,
}

/// <summary>How a packet is addressed.</summary>
internal enum MeshAddressKind
{
  /// <summary>Known node / system — directed path.</summary>
  Place = 0,
  /// <summary>Identity — flood; push into mailbox only when co-located with a node that holds it.</summary>
  Identity = 1,
  /// <summary>Named feed — flood to node caches; consumers pull by subscription (not pushed to mailbox).</summary>
  Feed = 2,
}

/// <summary>Who owns a mailbox / feed subscriptions.</summary>
internal enum MeshIdentityKind
{
  Person = 0,
  Household = 1,
  Firm = 2,
  Ship = 3,
  /// <summary>Facility, buoy, kiosk, drone rack — non-person endpoints.</summary>
  Thing = 4,
}
