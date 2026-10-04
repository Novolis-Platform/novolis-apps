using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Azure.Provisioning.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Reflection;

// Hours deliberately uses Podman for container-backed local development.
Environment.SetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME", "podman");

var builder = DistributedApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

var publishing = builder.ExecutionContext.IsPublishMode;

builder.AddAzureContainerAppEnvironment("hours-aca");

var administratorPassword = builder.AddParameter(
    "hours-initial-administrator-password",
    secret: true);

var storage = builder.AddAzureStorage("hours-storage");
if (!publishing)
{
    storage.RunAsEmulator(azurite =>
    {
        azurite.WithLifetime(ContainerLifetime.Persistent);
        azurite.WithDataVolume("novolis-hours-azurite-data");
        azurite.WithTablePort(10002);
    });
}

var tables = storage.AddTables("hours-tables");

var hoursServer = builder
    .AddProject<Projects.Novolis_Hours_Server>("hours-server")
    .WithReplicas(1)
    .WithEnvironment("Hours__InitialAdministratorPassword", administratorPassword)
    .WithEnvironment("Hours__EnableDemoAdminCredentials", "false")
    .WithEnvironment("Hours__UseInMemoryJournal", "false")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WithEnvironment("Hours__StorageProvider", "azure-tables")
    .WithEnvironment("Hours__AzureTablesConnectionString", tables)
    .WithEnvironment("Hours__AzureTablesTablePrefix", "novolis-hours")
    .WithEnvironment("Hours__RequireHttps", "true")
    .WithEnvironment("OTEL_SERVICE_NAME", "novolis-hours")
    .WithRoleAssignments(storage, StorageBuiltInRole.StorageTableDataContributor)
    .WithReference(tables)
    .WaitFor(tables)
    .PublishAsAzureContainerApp(static (_, app) =>
    {
        // The table journal has one authoritative writer.
        app.Template.Scale.MinReplicas = 1;
        app.Template.Scale.MaxReplicas = 1;
    });

if (publishing)
{
    hoursServer
        .WithHttpEndpoint(name: "http", targetPort: 8080)
        .WithExternalHttpEndpoints()
        .WithEnvironment("Hours__TrustedProxyAddresses__0", "*");
}
else
{
    hoursServer
        .WithHttpsEndpoint(name: "https", port: 5700)
        .WithExternalHttpEndpoints();
}

#pragma warning disable ASPIREPROBES001
var probeEndpoint = publishing ? "http" : "https";
hoursServer
    .WithHttpProbe(
        ProbeType.Startup,
        "/health/startup",
        initialDelaySeconds: 5,
        periodSeconds: 10,
        timeoutSeconds: 10,
        failureThreshold: 12,
        endpointName: probeEndpoint)
    .WithHttpProbe(
        ProbeType.Readiness,
        "/health/ready",
        periodSeconds: 10,
        timeoutSeconds: 10,
        failureThreshold: 3,
        endpointName: probeEndpoint)
    .WithHttpProbe(
        ProbeType.Liveness,
        "/health/live",
        periodSeconds: 30,
        timeoutSeconds: 5,
        failureThreshold: 3,
        endpointName: probeEndpoint);
#pragma warning restore ASPIREPROBES001

if (builder.Environment.IsDevelopment())
{
    var acceptanceSeedPassword = builder.AddParameter(
        "hours-acceptance-seed-password",
        secret: true);
    hoursServer
        .WithEnvironment("Hours__EnableAcceptanceSeed", "true")
        .WithEnvironment("Hours__AcceptanceSeedPassword", acceptanceSeedPassword);
}

var serverEndpoint = publishing
    ? hoursServer.GetEndpoint("http")
    : hoursServer.GetEndpoint("https");

builder.AddProject<Projects.Novolis_Hours_Client_Avalonia>("hours-client-avalonia")
    .WithEnvironment("NOVOLIS_HOURS_SERVICE_URL", serverEndpoint)
    .WithEnvironment("NOVOLIS_AVALONIA_AGENT", "1")
    .WithExplicitStart()
    .ExcludeFromManifest();

builder.AddProject<Projects.Novolis_Hours_Client_Maui>("hours-client-maui")
    .WithEnvironment("NOVOLIS_HOURS_SERVICE_URL", serverEndpoint)
    .WithEnvironment("NOVOLIS_MAUI_AGENT", "1")
    .WithExplicitStart()
    .ExcludeFromManifest();

var blazor = builder.AddProject<Projects.Novolis_Hours_Client_Blazor>("hours-client-blazor")
    .WithEnvironment("NOVOLIS_HOURS_SERVICE_URL", serverEndpoint)
    .WithExplicitStart()
    .PublishAsAzureContainerApp(static (_, app) =>
    {
        app.Template.Scale.MinReplicas = 1;
        app.Template.Scale.MaxReplicas = 1;
    });

if (publishing)
{
    blazor
        .WithHttpEndpoint(name: "http", targetPort: 8080)
        .WithExternalHttpEndpoints();
}
else
{
    blazor
        .WithHttpsEndpoint(name: "https", port: 5710)
        .WithExternalHttpEndpoints();
}

hoursServer.WithEnvironment(
    "Hours__AllowedClientOrigins__0",
    publishing ? blazor.GetEndpoint("http") : blazor.GetEndpoint("https"));

builder.AddProject<Projects.Novolis_Hours_Client_Cli>("hours-client-cli")
    .WithEnvironment("NOVOLIS_HOURS_SERVICE_URL", serverEndpoint)
    .WithExplicitStart()
    .ExcludeFromManifest();

builder.Build().Run();
