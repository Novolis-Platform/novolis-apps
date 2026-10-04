using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Hours.Server;

var demo = args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
var hostArguments = args
    .Where(argument =>
        !string.Equals(argument, "serve", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(argument, "--demo", StringComparison.OrdinalIgnoreCase))
    .ToArray();
var hasExplicitUrl = hostArguments.Any(argument =>
    string.Equals(argument, "--urls", StringComparison.OrdinalIgnoreCase) ||
    argument.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase));

var app = HoursServerApplication.Build(
    hostArguments,
    builder =>
    {
        if (!demo)
        {
            return;
        }

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Hours:EnableDemoAdminCredentials"] = bool.TrueString,
        });
        if (!hasExplicitUrl)
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        }
    });
await app.StartAsync();

var addresses = app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()?
    .Addresses
    .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];
foreach (var address in addresses)
{
    Console.WriteLine($"Novolis Hours is running at {address}");
}

if (demo)
{
    Console.WriteLine("Local demo sign-in: admin / admin. This mode is intended only for a local demonstration.");
}
else
{
    Console.WriteLine("Demo credentials are disabled. Supply an existing administrator or Hours__InitialAdministratorPassword.");
}
await app.WaitForShutdownAsync();
