using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novolis.Hours.Infrastructure;

namespace Novolis.Hours.Server;

/// <summary>Projects durable journal appends from a channel onto authenticated SignalR clients.</summary>
public sealed class HoursRealtimeProjectionService : BackgroundService
{
    private readonly HoursChangeFeed changeFeed;
    private readonly IHubContext<HoursHub> hub;
    private readonly ILogger<HoursRealtimeProjectionService> logger;

    /// <summary>Initializes the realtime projection worker.</summary>
    public HoursRealtimeProjectionService(
        HoursChangeFeed changeFeed,
        IHubContext<HoursHub> hub,
        ILogger<HoursRealtimeProjectionService> logger)
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
                using var activity = HoursTelemetry.ActivitySource.StartActivity("hours.realtime.project");
                await hub.Clients.Group(HoursHub.EmployeeGroup(entry.EmployeeId))
                    .SendAsync("hoursChanged", new
                    {
                        entry.EmployeeId,
                        entry.Type,
                        entry.OccurredAtUtc,
                    }, stoppingToken);
                await hub.Clients.Group(HoursHub.AdministratorsGroup)
                    .SendAsync("hoursChanged", new
                    {
                        entry.EmployeeId,
                        entry.Type,
                        entry.OccurredAtUtc,
                    }, stoppingToken);
                HoursTelemetry.RealtimeMessages.Add(2);
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
