using System.Net;
using System.Text.Json;
using ReadAloud.Services;

namespace ReadAloud.Android;

/// <summary>Discovers Azure subscriptions and compatible Speech resources for Android.</summary>
public sealed class AndroidAzureSpeechResourcePicker : IAzureSpeechResourcePicker
{
    const string ManagementBase = "https://management.azure.com";
    const string CognitiveServicesApiVersion = "2023-05-01";
    const string SubscriptionsApiVersion = "2020-01-01";

    readonly AndroidEntraAuthentication _authentication;
    readonly HttpClient _httpClient;
    readonly Dictionary<string, IReadOnlyList<AzureSpeechResourceChoice>> _resourceCache =
        new(StringComparer.OrdinalIgnoreCase);

    public AndroidAzureSpeechResourcePicker(
        AndroidEntraAuthentication authentication,
        HttpClient httpClient)
    {
        _authentication = authentication
            ?? throw new ArgumentNullException(nameof(authentication));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<IReadOnlyList<AzureSubscriptionChoice>> SignInAsync(
        CancellationToken cancellationToken = default)
    {
        await _authentication.SignInAsync(cancellationToken).ConfigureAwait(false);
        _resourceCache.Clear();
        return await GetSubscriptionsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AzureSpeechResourceGroupChoice>> GetSpeechResourceGroupsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var resources = await GetSpeechResourcesForSubscriptionAsync(
                subscriptionId,
                cancellationToken)
            .ConfigureAwait(false);

        return resources
            .GroupBy(resource => resource.ResourceGroupName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AzureSpeechResourceGroupChoice(group.Key, group.Count()))
            .ToArray();
    }

    public async Task<IReadOnlyList<AzureSpeechResourceChoice>> GetSpeechResourcesAsync(
        string subscriptionId,
        string resourceGroupName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceGroupName);

        var resources = await GetSpeechResourcesForSubscriptionAsync(
                subscriptionId,
                cancellationToken)
            .ConfigureAwait(false);
        return resources
            .Where(resource =>
                string.Equals(
                    resource.ResourceGroupName,
                    resourceGroupName,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(resource => resource.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        _resourceCache.Clear();
        await _authentication.SignOutAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task<IReadOnlyList<AzureSubscriptionChoice>> GetSubscriptionsAsync(
        CancellationToken cancellationToken)
    {
        var url =
            $"{ManagementBase}/subscriptions?api-version={SubscriptionsApiVersion}";
        var elements = await GetAllAsync(url, cancellationToken).ConfigureAwait(false);
        return elements
            .Select(element => new
            {
                Id = StringProperty(element, "subscriptionId"),
                Name = StringProperty(element, "displayName"),
                State = StringProperty(element, "state"),
            })
            .Where(subscription =>
                !string.IsNullOrWhiteSpace(subscription.Id) &&
                string.Equals(subscription.State, "Enabled", StringComparison.OrdinalIgnoreCase))
            .Select(subscription => new AzureSubscriptionChoice(
                subscription.Id!,
                string.IsNullOrWhiteSpace(subscription.Name)
                    ? subscription.Id!
                    : subscription.Name!))
            .OrderBy(subscription => subscription.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    async Task<IReadOnlyList<AzureSpeechResourceChoice>> GetSpeechResourcesForSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        if (_resourceCache.TryGetValue(subscriptionId, out var cached))
            return cached;

        var url =
            $"{ManagementBase}/subscriptions/{Uri.EscapeDataString(subscriptionId)}" +
            $"/providers/Microsoft.CognitiveServices/accounts?api-version={CognitiveServicesApiVersion}";
        var elements = await GetAllAsync(url, cancellationToken).ConfigureAwait(false);
        var resources = elements
            .Where(IsCompatibleSpeechKind)
            .Select(element => CreateResourceChoice(element, subscriptionId))
            .Where(resource => resource is not null)
            .Select(resource => resource!)
            .OrderBy(resource => resource.ResourceGroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(resource => resource.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _resourceCache[subscriptionId] = resources;
        return resources;
    }

    async Task<IReadOnlyList<JsonElement>> GetAllAsync(
        string initialUrl,
        CancellationToken cancellationToken)
    {
        var elements = new List<JsonElement>();
        var url = initialUrl;
        while (!string.IsNullOrWhiteSpace(url))
        {
            using var request = await CreateRequestAsync(url, cancellationToken)
                .ConfigureAwait(false);
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized =>
                        "Azure sign-in expired. Sign in again.",
                    HttpStatusCode.Forbidden =>
                        "This Azure account cannot discover resources in the selected scope.",
                    _ =>
                        $"Azure resource discovery failed ({(int)response.StatusCode}).",
                };
                throw new InvalidOperationException(message);
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var document = await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (document.RootElement.TryGetProperty("value", out var value) &&
                value.ValueKind == JsonValueKind.Array)
            {
                elements.AddRange(value.EnumerateArray().Select(element => element.Clone()));
            }

            url = StringProperty(document.RootElement, "nextLink") ?? string.Empty;
        }

        return elements;
    }

    async Task<HttpRequestMessage> CreateRequestAsync(
        string url,
        CancellationToken cancellationToken)
    {
        var token = await _authentication
            .GetTokenAsync([AndroidEntraAuthentication.ManagementScope], cancellationToken)
            .ConfigureAwait(false);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        return request;
    }

    AzureSpeechResourceChoice? CreateResourceChoice(
        JsonElement element,
        string subscriptionId)
    {
        var id = StringProperty(element, "id");
        var name = StringProperty(element, "name");
        var location = StringProperty(element, "location");
        var endpoint = element.TryGetProperty("properties", out var properties)
            ? StringProperty(properties, "endpoint")
            : null;
        var resourceGroupName = GetResourceGroupName(id);
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(location) ||
            string.IsNullOrWhiteSpace(resourceGroupName) ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            !string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new AzureSpeechResourceChoice(
            subscriptionId,
            resourceGroupName,
            name,
            endpointUri,
            location,
            _authentication.ClientId,
            _authentication.TenantId);
    }

    static bool IsCompatibleSpeechKind(JsonElement element)
    {
        var kind = StringProperty(element, "kind");
        return string.Equals(kind, "SpeechServices", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "AIServices", StringComparison.OrdinalIgnoreCase);
    }

    static string? GetResourceGroupName(string? resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            return null;

        var segments = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index + 1 < segments.Length; index++)
        {
            if (string.Equals(segments[index], "resourceGroups", StringComparison.OrdinalIgnoreCase))
                return segments[index + 1];
        }

        return null;
    }

    static string? StringProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
