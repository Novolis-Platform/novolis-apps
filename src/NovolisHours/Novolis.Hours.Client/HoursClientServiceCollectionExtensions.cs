using Microsoft.Extensions.DependencyInjection;
using Novolis.Http.Client;

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

        services.AddNovolisHttp();
        services.AddTransient<HoursSessionHandler>();
        return services
            .AddHttpClientFor<HoursHttpClientKey>(client =>
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
