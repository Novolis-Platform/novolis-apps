namespace ReadAloud.Services;

/// <summary>A subscription visible to the signed-in Azure account.</summary>
public sealed record AzureSubscriptionChoice(
    string Id,
    string DisplayName);
