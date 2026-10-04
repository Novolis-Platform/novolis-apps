using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novolis.Hours.Domain;
using Novolis.Hours.Storage;

namespace Novolis.Hours.Server;

/// <summary>Projects durable journal appends from a channel onto authenticated SignalR clients.</summary>
public sealed class HoursServerRealtimeProjectionService : BackgroundService
{
    private readonly HoursChangeFeed changeFeed;
    private readonly IHubContext<HoursServerHub> hub;
    private readonly ILogger<HoursServerRealtimeProjectionService> logger;

    /// <summary>Initializes the realtime projection worker.</summary>
    public HoursServerRealtimeProjectionService(
        HoursChangeFeed changeFeed,
        IHubContext<HoursServerHub> hub,
        ILogger<HoursServerRealtimeProjectionService> logger)
    {
        this.changeFeed = changeFeed ?? throw new ArgumentNullException(nameof(changeFeed));
        this.hub = hub ?? throw new ArgumentNullException(nameof(hub));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var entry in changeFeed.Reader.ReadAllAsync(stoppingToken))
            {
                using var activity = HoursServerTelemetry.ActivitySource.StartActivity("hours.realtime.project");
                await hub.Clients.Group(HoursServerHub.EmployeeGroup(entry.EmployeeId))
                    .SendAsync("hoursChanged", new
                    {
                        entry.Id,
                        entry.EmployeeId,
                        entry.Type,
                        entry.OccurredAtUtc,
                        Projection = entry.Type == HoursEventType.WorkRegistrationRecorded
                            ? "work-registration"
                            : "legacy-or-other",
                    }, stoppingToken);
                await hub.Clients.Group(HoursServerHub.AdministratorsGroup)
                    .SendAsync("hoursChanged", new
                    {
                        entry.Id,
                        entry.EmployeeId,
                        entry.Type,
                        entry.OccurredAtUtc,
                        Projection = entry.Type == HoursEventType.WorkRegistrationRecorded
                            ? "work-registration"
                            : "legacy-or-other",
                    }, stoppingToken);
                HoursServerTelemetry.RealtimeMessages.Add(2);
                logger.LogInformation(
                    "Projected Hours event {HoursEventType} for employee {EmployeeId}.",
                    entry.Type,
                    entry.EmployeeId);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
