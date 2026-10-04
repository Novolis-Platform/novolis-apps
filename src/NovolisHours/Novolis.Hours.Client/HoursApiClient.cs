using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Hours.Contracts;
using Novolis.Http.Client;

namespace Novolis.Hours.Client;

/// <summary>Authenticated HTTP client for the Novolis Hours host API.</summary>
public sealed class HoursApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient httpClient;
    private readonly IDisposable? lifetime;
    private readonly Action<HttpRequestMessage>? configureRequest;
    private string? antiforgeryToken;

    /// <summary>Initializes the client over an already configured HTTP client.</summary>
    public HoursApiClient(
        HttpClient httpClient,
        Action<HttpRequestMessage>? configureRequest = null)
        : this(httpClient, lifetime: null, configureRequest)
    {
    }

    private HoursApiClient(
        HttpClient httpClient,
        IDisposable? lifetime,
        Action<HttpRequestMessage>? configureRequest)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.lifetime = lifetime;
        this.configureRequest = configureRequest;
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

        var services = new ServiceCollection();
        services.AddHoursApiClient(serviceUri);
        var provider = services.BuildServiceProvider();
        var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient<HoursHttpClientKey>();
        return new HoursApiClient(httpClient, provider, configureRequest: null);
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
        using var response = await SendAsync(request, cancellationToken);
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
        using var response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "api/auth/me"),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The Hours host returned an empty current-user response.");
        return new HoursClientUser(payload.EmployeeId, payload.DisplayName, payload.Role, false);
    }

    /// <summary>Ends the current authenticated session.</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetAntiforgeryTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
        request.Headers.Add("X-Novolis-Hours-CSRF", token);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        antiforgeryToken = null;
    }

    /// <summary>Gets a lightweight live summary for an employee who the active user may view.</summary>
    public async Task<HoursEmployeeSummary> GetEmployeeSummaryAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        using var response = await SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/employees/{Uri.EscapeDataString(employeeId)}/view"),
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
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>Records a v2 immutable work assertion through the protected API.</summary>
    public async Task<WorkRegistrationResponse> RecordWorkRegistrationAsync(
        RecordWorkRegistrationRequest registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var token = await GetAntiforgeryTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "api/v2/work-registrations")
        {
            Content = JsonContent.Create(registration, options: JsonOptions),
        };
        request.Headers.Add("X-Novolis-Hours-CSRF", token);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<WorkRegistrationResponse>(
                JsonOptions,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The Hours host returned an empty work-registration response.");
    }

    /// <summary>Appends a manual Dimension assignment to resolved work.</summary>
    public async Task RecordDimensionAssignmentAsync(
        string employeeId,
        RecordDimensionAssignmentRequest assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        ArgumentNullException.ThrowIfNull(assignment);
        var token = await GetAntiforgeryTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/v2/employees/{Uri.EscapeDataString(employeeId)}/dimension-assignments")
        {
            Content = JsonContent.Create(assignment, options: JsonOptions),
        };
        request.Headers.Add("X-Novolis-Hours-CSRF", token);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>Publishes and retains the effective configuration snapshot for an employee.</summary>
    public async Task<ConfigurationSnapshotResponse> PublishConfigurationAsync(
        string employeeId,
        PublishConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        ArgumentNullException.ThrowIfNull(request);
        var token = await GetAntiforgeryTokenAsync(cancellationToken);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/v2/employees/{Uri.EscapeDataString(employeeId)}/configuration/publications")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.Add("X-Novolis-Hours-CSRF", token);
        using var response = await SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ConfigurationSnapshotResponse>(
                JsonOptions,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The Hours host returned an empty configuration snapshot response.");
    }

    /// <summary>Reads immutable v2 work assertions for a permitted employee.</summary>
    public async Task<IReadOnlyList<WorkRegistrationResponse>> GetWorkRegistrationsAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        using var response = await SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/v2/employees/{Uri.EscapeDataString(employeeId)}/work-registrations"),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<WorkRegistrationResponse[]>(
                JsonOptions,
                cancellationToken)
            ?? [];
    }

    /// <summary>Reads the append-only audit references for a permitted employee.</summary>
    public async Task<IReadOnlyList<HoursAuditEventResponse>> GetAuditAsync(
        string employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        using var response = await SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/v2/employees/{Uri.EscapeDataString(employeeId)}/audit"),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<HoursAuditEventResponse[]>(
                JsonOptions,
                cancellationToken)
            ?? [];
    }

    /// <summary>Reads the explainable WorkDay projection for a permitted employee.</summary>
    public async Task<WorkDayResponse> GetWorkDayAsync(
        string employeeId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        using var response = await SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/v2/employees/{Uri.EscapeDataString(employeeId)}/workdays/{date:yyyy-MM-dd}"),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<WorkDayResponse>(
                JsonOptions,
                cancellationToken)
            ?? throw new InvalidOperationException("The Hours host returned an empty WorkDay response.");
    }

    /// <summary>Reads a contiguous range of WorkDay projections for a permitted employee.</summary>
    public async Task<IReadOnlyList<WorkDayResponse>> GetWorkDaysAsync(
        string employeeId,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);
        using var response = await SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/v2/employees/{Uri.EscapeDataString(employeeId)}/workdays?from={from:yyyy-MM-dd}&through={through:yyyy-MM-dd}"),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<WorkDayResponse[]>(
                JsonOptions,
                cancellationToken)
            ?? [];
    }

    /// <summary>Reads a rebuildable review projection.</summary>
    public async Task<ReviewProjectionResponse> GetReviewAsync(
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"api/v2/reviews/{periodId:D}"),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ReviewProjectionResponse>(
                JsonOptions,
                cancellationToken)
            ?? throw new InvalidOperationException("The Hours host returned an empty review response.");
    }

    /// <summary>Creates a review period.</summary>
    public async Task<Guid> CreateReviewPeriodAsync(
        CreateReviewPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = await GetAntiforgeryTokenAsync(cancellationToken);
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v2/reviews")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.Add("X-Novolis-Hours-CSRF", token);
        using var response = await SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var period = await response.Content.ReadFromJsonAsync<ReviewPeriodResponse>(
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("The Hours host returned an empty review-period response.");
        return period.Id;
    }

    /// <summary>Appends a review action.</summary>
    public async Task<ReviewActionResponse> RecordReviewActionAsync(
        Guid periodId,
        RecordReviewActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = await GetAntiforgeryTokenAsync(cancellationToken);
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/v2/reviews/{periodId:D}/actions")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.Add("X-Novolis-Hours-CSRF", token);
        using var response = await SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ReviewActionResponse>(
                JsonOptions,
                cancellationToken)
            ?? throw new InvalidOperationException("The Hours host returned an empty review-action response.");
    }

    /// <summary>Reads the organisation Health Concerns projection.</summary>
    public async Task<HealthConcernsReportResponse> GetHealthReportAsync(
        string? teamId = null,
        string? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildReportQuery(teamId, employeeId);
        using var response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"api/v2/reports/health{query}"),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<HealthConcernsReportResponse>(
                JsonOptions,
                cancellationToken)
            ?? throw new InvalidOperationException("The Hours host returned an empty health report.");
    }

    /// <summary>Reads the temporal-overlap business-pressure projection.</summary>
    public async Task<BusinessPressureReportResponse> GetBusinessPressureReportAsync(
        string? teamId = null,
        string? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildReportQuery(teamId, employeeId);
        using var response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"api/v2/reports/business-pressure{query}"),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<BusinessPressureReportResponse>(
                JsonOptions,
                cancellationToken)
            ?? throw new InvalidOperationException("The Hours host returned an empty business-pressure report.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        httpClient.Dispose();
        lifetime?.Dispose();
    }

    private async Task<string> GetAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(antiforgeryToken))
        {
            return antiforgeryToken;
        }

        using var response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "api/auth/antiforgery"),
            cancellationToken);
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

    /// <summary>Normalizes a Hours service URI so relative API paths resolve correctly.</summary>
    internal static Uri EnsureTrailingSlash(Uri serviceUri) =>
        serviceUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? serviceUri
            : new Uri($"{serviceUri.AbsoluteUri}/", UriKind.Absolute);

    private static string BuildReportQuery(string? teamId, string? employeeId)
    {
        var values = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(teamId))
        {
            values.Add($"teamId={Uri.EscapeDataString(teamId)}");
        }

        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            values.Add($"employeeId={Uri.EscapeDataString(employeeId)}");
        }

        return values.Count == 0
            ? string.Empty
            : $"?{string.Join("&", values)}";
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        configureRequest?.Invoke(request);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private sealed record ReviewPeriodResponse(
        Guid Id,
        string EmployeeId,
        DateOnly From,
        DateOnly Through);
}
