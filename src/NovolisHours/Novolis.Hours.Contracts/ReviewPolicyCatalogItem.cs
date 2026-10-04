using System.Collections.Immutable;

namespace Novolis.Hours.Contracts;

/// <summary>Review policy an administrator can assign to a workplace.</summary>
public sealed record ReviewPolicyCatalogItem(
    string Id,
    bool AllowsDispute,
    ImmutableArray<ReviewStageCatalogItem> Stages);
