namespace ReadAloud.Services;

/// <summary>A subscription visible to the signed-in Azure account.</summary>
public sealed record AzureSubscriptionChoice(
    string Id,
    string DisplayName);

/// <summary>A resource group containing at least one compatible Speech resource.</summary>
public sealed record AzureSpeechResourceGroupChoice(
    string Name,
    int SpeechResourceCount);

/// <summary>A selectable Azure Speech resource.</summary>
public sealed record AzureSpeechResourceChoice(
    string SubscriptionId,
    string ResourceGroupName,
    string Name,
    Uri Endpoint,
    string Location,
    string ClientId,
    string TenantId);

/// <summary>Azure Monitor usage totals for one Speech resource and time window.</summary>
public sealed record AzureSpeechUsageSnapshot(
    DateTimeOffset Start,
    DateTimeOffset End,
    long? SynthesizedCharacters,
    long? TotalCalls,
    long? SuccessfulCalls,
    long? ClientErrors,
    long? ServerErrors);

/// <summary>
/// Discovers user-visible Azure Speech resources after interactive sign-in.
/// Platform hosts provide the implementation because authentication and browser
/// callbacks are platform-specific.
/// </summary>
public interface IAzureSpeechResourcePicker
{
    /// <summary>Signs in and returns enabled subscriptions visible to the account.</summary>
    Task<IReadOnlyList<AzureSubscriptionChoice>> SignInAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Returns only groups containing compatible Speech resources.</summary>
    Task<IReadOnlyList<AzureSpeechResourceGroupChoice>> GetSpeechResourceGroupsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns compatible Speech resources in the selected group.</summary>
    Task<IReadOnlyList<AzureSpeechResourceChoice>> GetSpeechResourcesAsync(
        string subscriptionId,
        string resourceGroupName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads Azure Monitor usage for the selected resource. The endpoint is used
    /// to recover the resource identity after an app restart.
    /// </summary>
    Task<AzureSpeechUsageSnapshot> GetUsageAsync(
        Uri endpoint,
        CancellationToken cancellationToken = default);

    /// <summary>Signs out and removes the platform token cache.</summary>
    Task SignOutAsync(CancellationToken cancellationToken = default);
}
