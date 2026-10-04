using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;

namespace Novolis.Hours.Client.Blazor;

/// <summary>Scoped browser session that keeps authentication and API access consistent across routes.</summary>
public sealed class HoursBrowserSession : IDisposable
{
    private const string ServiceUrlStorageKey = "hours-service-url";
    private readonly IConfiguration configuration;
    private readonly IJSRuntime js;

    /// <summary>Initializes a browser session from the configured Hours service endpoint.</summary>
    public HoursBrowserSession(IConfiguration configuration, IJSRuntime js)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.js = js ?? throw new ArgumentNullException(nameof(js));
        ServiceUrl = configuration["HoursServiceUrl"] ?? "https://localhost:5700/";
    }

    /// <summary>Configured absolute service URL.</summary>
    public string ServiceUrl { get; set; }

    /// <summary>Raised after sign-in, sign-out, or session restore so chrome can refresh.</summary>
    public event Action? Changed;

    /// <summary>Current authenticated user, when signed in.</summary>
    public HoursClientUser? CurrentUser { get; private set; }

    /// <summary>Authenticated API client, when signed in.</summary>
    public HoursApiClient? ApiClient { get; private set; }

    /// <summary>Gets whether the browser is restoring an existing server session.</summary>
    public bool IsRestoring { get; private set; }

    /// <summary>Restores the signed-in identity after a browser refresh or direct route navigation.</summary>
    public Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentUser is not null || ApiClient is not null || IsRestoring)
        {
            return restoreTask ?? Task.CompletedTask;
        }

        return restoreTask ??= RestoreCoreAsync(cancellationToken);
    }

    private async Task RestoreCoreAsync(CancellationToken cancellationToken)
    {
        IsRestoring = true;
        HoursApiClient? client = null;
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", ServiceUrlStorageKey);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                ServiceUrl = stored;
            }

            if (!Uri.TryCreate(ServiceUrl, UriKind.Absolute, out var endpoint))
            {
                return;
            }

            client = new HoursApiClient(
                new HttpClient { BaseAddress = EnsureTrailingSlash(endpoint) },
                request => request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include));
            CurrentUser = await client.GetCurrentUserAsync(cancellationToken);
            ApiClient = client;
            client = null;
            NotifyChanged();
        }
        catch
        {
            client?.Dispose();
        }
        finally
        {
            IsRestoring = false;
            restoreTask = null;
        }
    }

    /// <summary>Signs in through the real cookie and antiforgery flow.</summary>
    public async Task SignInAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(ServiceUrl, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("Enter an absolute Hours service URL.");
        }

        ApiClient?.Dispose();
        ApiClient = new HoursApiClient(
            new HttpClient { BaseAddress = EnsureTrailingSlash(endpoint) },
            request => request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include));
        CurrentUser = await ApiClient.SignInAsync(login, password, cancellationToken);
        await PersistServiceUrlAsync();
        NotifyChanged();
    }

    /// <summary>Signs out and clears the authenticated browser session.</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (ApiClient is not null)
        {
            try
            {
                await ApiClient.SignOutAsync(cancellationToken);
            }
            finally
            {
                ApiClient.Dispose();
                ApiClient = null;
            }
        }

        CurrentUser = null;
        NotifyChanged();
    }

    /// <summary>Gets the configured service endpoint for diagnostics.</summary>
    public string GetConfiguredServiceUrl() =>
        configuration["HoursServiceUrl"] ?? ServiceUrl;

    /// <summary>Persists the Hours service URL for the next visit.</summary>
    public Task PersistServiceUrlAsync() =>
        js.InvokeVoidAsync("localStorage.setItem", ServiceUrlStorageKey, ServiceUrl).AsTask();

    /// <summary>Builds a local timestamp in the Norwegian acceptance calendar.</summary>
    public static DateTimeOffset ToNominalTimestamp(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ApiClient?.Dispose();
        ApiClient = null;
        CurrentUser = null;
    }

    private void NotifyChanged() => Changed?.Invoke();

    private static Uri EnsureTrailingSlash(Uri serviceUri) =>
        serviceUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? serviceUri
            : new Uri($"{serviceUri.AbsoluteUri}/", UriKind.Absolute);

    private Task? restoreTask;
}
