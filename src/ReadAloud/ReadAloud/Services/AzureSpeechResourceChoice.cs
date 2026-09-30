namespace ReadAloud.Services;

/// <summary>A selectable Azure Speech resource.</summary>
public sealed record AzureSpeechResourceChoice(
    string SubscriptionId,
    string ResourceGroupName,
    string Name,
    Uri Endpoint,
    string Location,
    string ClientId,
    string TenantId);
