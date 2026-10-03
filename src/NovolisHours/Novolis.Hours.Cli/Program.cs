using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Hours.Server;

var app = HoursApplication.Build(args);
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

Console.WriteLine("Local demo sign-in: admin / admin (disable it before deployment).");
await app.WaitForShutdownAsync();
