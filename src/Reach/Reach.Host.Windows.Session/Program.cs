using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Novolis.Reach.Host.Windows.Session;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton<ReachSessionHost>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ReachSessionHost>());
        using var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}
