using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novolis.Hours.Domain;

namespace Novolis.Hours.Server;

/// <summary>Proposes overdue cadence-based positive-flex normalizations without committing them or blocking work registration.</summary>
public sealed class HoursServerFlexSettlementScheduler : BackgroundService
{
    private readonly IHoursJournal journal;
    private readonly IHoursPolicyProvider policies;
    private readonly HoursService hours;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<HoursServerFlexSettlementScheduler> logger;

    /// <summary>Initializes the scheduler over the same journal and policy resolver as interactive application use cases.</summary>
    public HoursServerFlexSettlementScheduler(
        IHoursJournal journal,
        IHoursPolicyProvider policies,
        HoursService hours,
        TimeProvider timeProvider,
        ILogger<HoursServerFlexSettlementScheduler> logger)
    {
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
        this.hours = hours ?? throw new ArgumentNullException(nameof(hours));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Assesses all known employments once; exposed for deterministic full-product feature tests.</summary>
    public async Task AssessOnceAsync(CancellationToken cancellationToken = default)
    {
        var entries = await journal.ReadAllAsync(cancellationToken);
        var assessedOn = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        foreach (var employeeId in entries
                     .Select(item => item.EmployeeId)
                     .Distinct(StringComparer.Ordinal))
        {
            var cutoff = policies.GetPolicy(employeeId).SettlementPolicy.MostRecentClosedDate(assessedOn);
            if (cutoff is null)
            {
                continue;
            }

            var proposal = await hours.ProposeFlexNormalizationAsync(
                employeeId,
                cutoff.Value,
                HoursActor.System(),
                cancellationToken);
            if (proposal is not null)
            {
                logger.LogInformation(
                    "Proposed flex normalization {HoursAdjustmentId} for employee {EmployeeId} at closing date {ClosingDate}.",
                    proposal.Id,
                    employeeId,
                    cutoff.Value);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await AssessOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(24), timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Hours flex settlement scheduler failed its non-blocking assessment.");
        }
    }
}
