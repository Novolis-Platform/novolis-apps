namespace Novolis.Hours.Contracts;

/// <summary>One review stage shown on the setup walkthrough.</summary>
public sealed record ReviewStageCatalogItem(
    string Id,
    string RequiredAction,
    string Role,
    int? ApprovalLevel);
