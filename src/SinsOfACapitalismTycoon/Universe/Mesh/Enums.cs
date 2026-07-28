namespace SinsOfACapitalismTycoon.Universe.Mesh;

/// <summary>Traffic layer — pulse is sprint drones; bulk is freight-class; public is flood record.</summary>
public enum MeshTrafficLayer
{
  Pulse = 0,
  Bulk = 1,
  Public = 2,
}

/// <summary>How a packet is addressed.</summary>
public enum MeshAddressKind
{
  /// <summary>Known hub / system — directed path.</summary>
  Place = 0,
  /// <summary>Mobile identity — flood until mailboxed.</summary>
  Identity = 1,
  /// <summary>Civilization-wide record — flood to all hubs.</summary>
  Public = 2,
}
