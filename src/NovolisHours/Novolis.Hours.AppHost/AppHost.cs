using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using System.Reflection;

// Hours deliberately uses Podman for container-backed local development.
Environment.SetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME", "podman");

var builder = DistributedApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

var administratorPassword = builder.AddParameter(
    "hours-initial-administrator-password",
    secret: true);

var azurite = builder
    .AddContainer("hours-azurite", "mcr.microsoft.com/azure-storage/azurite", "3.35.0")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEntrypoint("azurite")
    .WithArgs(
        "--tableHost",
        "0.0.0.0",
        "--tablePort",
        "10002",
        "--location",
        "/data",
        "--skipApiVersionCheck")
    .WithVolume("novolis-hours-azurite-data", "/data")
    .WithEndpoint(targetPort: 10002, port: 10002, name: "table")
    .WithEndpointProxySupport(false);

var hours = builder
    .AddProject<Projects.Novolis_Hours_Server>("hours")
    .WithReplicas(1)
    .WithEnvironment(
        "Hours__DataPath",
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "Hours",
            "Aspire"))
    .WithEnvironment("Hours__InitialAdministratorPassword", administratorPassword)
    .WithEnvironment("Hours__EnableDemoAdminCredentials", "false")
    .WithEnvironment("Hours__UseInMemoryJournal", "false")
    .WithEnvironment("Hours__StorageProvider", "azure-tables")
    .WithEnvironment("Hours__AzureTablesConnectionString", "UseDevelopmentStorage=true")
    .WithEnvironment("Hours__AzureTablesTablePrefix", "novolis-hours")
    .WithEnvironment("Hours__AllowedClientOrigins__0", "https://localhost:5710")
    .WithEnvironment("Hours__RequireHttps", "true")
    .WithEnvironment("OTEL_SERVICE_NAME", "novolis-hours")
    .WithHttpsEndpoint(name: "https", port: 5700)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health/ready")
    .WaitFor(azurite);

// Keep one server instance while the Azure Table journal is the authoritative writer.
_ = hours;

builder.AddProject<Projects.Novolis_Hours_Client_Avalonia>("hours-client-avalonia")
    .WithEnvironment("NOVOLIS_HOURS_SERVICE_URL", hours.GetEndpoint("https"))
    .WithEnvironment("NOVOLIS_AVALONIA_AGENT", "1")
    .WithExplicitStart();

builder.AddProject<Projects.Novolis_Hours_Client_Maui>("hours-client-maui")
    .WithEnvironment("NOVOLIS_HOURS_SERVICE_URL", hours.GetEndpoint("https"))
    .WithEnvironment("NOVOLIS_MAUI_AGENT", "1")
    .WithExplicitStart();

builder.AddProject<Projects.Novolis_Hours_Client_Blazor>("hours-client-blazor")
    .WithEnvironment("NOVOLIS_HOURS_SERVICE_URL", hours.GetEndpoint("https"))
    .WithHttpsEndpoint(name: "https", port: 5710)
    .WithExternalHttpEndpoints()
    .WithExplicitStart();

builder.Build().Run();
