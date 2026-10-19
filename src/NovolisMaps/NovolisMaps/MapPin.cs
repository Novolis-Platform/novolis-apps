using Novolis.Math.Geometry;

namespace NovolisMaps;

/// <summary>A user-owned map pin persisted by Novolis Maps.</summary>
internal sealed record MapPin(Guid Id, string Label, GeoCoordinate Coordinate);
