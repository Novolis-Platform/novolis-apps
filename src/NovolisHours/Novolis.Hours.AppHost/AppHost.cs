using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using System.Reflection;

var builder = DistributedApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

var administratorPassword = builder.AddParameter(
    "hours-initial-administrator-password",
    secret: true);

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
    .WithEnvironment("Hours__RequireHttps", "true")
    .WithEnvironment("OTEL_SERVICE_NAME", "novolis-hours")
    .WithHttpsEndpoint(name: "https")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health/ready");

// Keep one server instance while the JSON journal is the authoritative writer.
_ = hours;

builder.AddProject<Projects.Novolis_Hours_Client_Avalonia>("hours-client-avalonia")
    .WithEnvironment("NOVOLIS_HOURS_SERVICE_URL", hours.GetEndpoint("https"))
    .WithExplicitStart();

builder.AddProject<Projects.Novolis_Hours_Client_Maui>("hours-client-maui")
    .WithEnvironment("NOVOLIS_HOURS_SERVICE_URL", hours.GetEndpoint("https"))
    .WithExplicitStart();

builder.Build().Run();
