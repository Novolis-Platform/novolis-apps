using System.Collections.Immutable;

namespace Novolis.Hours.Contracts;

/// <summary>Configured Dimension that a person may paint onto actual work.</summary>
public sealed record DimensionBrushResponse(
    string Id,
    string Name,
    ImmutableArray<DimensionBrushValueResponse> Values);
