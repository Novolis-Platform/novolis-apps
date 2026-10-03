using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Hours.Client;

/// <summary>Authenticated HTTP client for the Novolis Hours host API.</summary>
public sealed class HoursApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient httpClient;
    private string? antiforgeryToken;

    /// <summary>Initializes the client over an already configured HTTP client.</summary>
    public HoursApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (this.httpClient.BaseAddress is null)
        {
            throw new ArgumentException("The HTTP client must have a base address.", nameof(httpClient));
        }
    }

    /// <summary>Creates a network client with a private cookie session for one user.</summary>
    public static HoursApiClient Connect(Uri serviceUri)
    {
        ArgumentNullException.ThrowIfNull(serviceUri);
        if (!serviceUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The Hours service URI must be absolute.", nameof(serviceUri));
        }

        var transport = new HoursSessionHandler(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
        });
        var httpClient = new HttpClient(transport, disposeHandler: true)
        {
            BaseAddress = EnsureTrailingSlash(serviceUri),
        };
        return new HoursApiClient(httpClient);
    }

    /// <summary>Signs in and returns the authenticated Hours role profile.</summary>
    public async Task<HoursClientUser> SignInAsync(
        string login,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var token = await GetAntiforgeryTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(login, password), options: JsonOptions),
        };
        request.Headers.Add("X-Novolis-Hours-CSRF", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The Hours host returned an empty login response.");
        antiforgeryToken = null;
        await GetAntiforgeryTokenAsync(cancellationToken);
        return new HoursClientUser(
            payload.EmployeeId,
            payload.DisplayName,
            payload.Role,
            payload.IsDemoAdministrator);
    }

    /// <summary>Gets the profile associated with the active Hours session.</summary>
    public async Task<HoursClientUser> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/auth/me", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The Hours host returned an empty current-user response.");
        return new HoursClientUser(payload.EmployeeId, payload.DisplayName, payload.Role, false);
    }

    /// <summary>Gets a lightweight live summary for an employee who the active user may view.</summary>
    public async Task<HoursEmployeeSummary> GetEmployeeSummaryAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        using var response = await httpClient.GetAsync(
            $"api/employees/{Uri.EscapeDataString(employeeId)}/view",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = document.RootElement;
        return new HoursEmployeeSummary(
            root.GetProperty("employeeId").GetString() ?? employeeId,
            TimeSpan.Parse(
                root.GetProperty("flexSaldo").GetString() ?? "00:00:00",
                CultureInfo.InvariantCulture),
            root.GetProperty("entries").GetArrayLength(),
            root.GetProperty("adjustments").GetArrayLength(),
            root.GetProperty("anomalies").GetArrayLength());
    }

    /// <summary>Posts an actual-presence record through the protected Hours API.</summary>
    public async Task RegisterWorkAsync(
        HoursWorkRegistration registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var token = await GetAntiforgeryTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/work")
        {
            Content = JsonContent.Create(registration, options: JsonOptions),
        };
        request.Headers.Add("X-Novolis-Hours-CSRF", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => httpClient.Dispose();

    private async Task<string> GetAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(antiforgeryToken))
        {
            return antiforgeryToken;
        }

        using var response = await httpClient.GetAsync("api/auth/antiforgery", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The Hours host returned an empty antiforgery response.");
        if (string.IsNullOrWhiteSpace(payload.Token))
        {
            throw new InvalidOperationException("The Hours host returned an empty antiforgery token.");
        }

        antiforgeryToken = payload.Token;
        return antiforgeryToken;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"The Hours host responded with {(int)response.StatusCode} ({response.ReasonPhrase}): {details}",
            null,
            response.StatusCode);
    }

    private static Uri EnsureTrailingSlash(Uri serviceUri) =>
        serviceUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? serviceUri
            : new Uri($"{serviceUri.AbsoluteUri}/", UriKind.Absolute);

    private sealed record AntiforgeryResponse(string Token);

    private sealed record LoginRequest(string Login, string Password);

    private sealed record LoginResponse(
        string EmployeeId,
        string DisplayName,
        HoursClientRole Role,
        bool IsDemoAdministrator);

    private sealed record CurrentUserResponse(string EmployeeId, string DisplayName, HoursClientRole Role);
}
