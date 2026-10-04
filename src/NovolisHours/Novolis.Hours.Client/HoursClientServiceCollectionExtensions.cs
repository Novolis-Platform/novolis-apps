using Microsoft.Extensions.DependencyInjection;

namespace Novolis.Hours.Client;

/// <summary>Registers the Hours API client on <see cref="IHttpClientFactory"/>.</summary>
public static class HoursClientServiceCollectionExtensions
{
    /// <summary>Adds a named Hours HTTP client with a private cookie session.</summary>
    public static IHttpClientBuilder AddHoursApiClient(
        this IServiceCollection services,
        Uri serviceUri)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceUri);
        if (!serviceUri.IsAbsoluteUri)
        {
            throw new ArgumentException("The Hours service URI must be absolute.", nameof(serviceUri));
        }

        services.AddTransient<HoursSessionHandler>();
        return services
            .AddHttpClient(HoursHttpClientKey.HttpClientName, client =>
            {
                client.BaseAddress = HoursApiClient.EnsureTrailingSlash(serviceUri);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            })
            .AddHttpMessageHandler<HoursSessionHandler>();
    }
}
