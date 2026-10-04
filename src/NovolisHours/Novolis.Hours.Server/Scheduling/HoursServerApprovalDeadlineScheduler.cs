using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Novolis.Hours.Domain;
using Novolis.WorkflowEngine;

namespace Novolis.Hours.Server;

/// <summary>Daily workflow scheduler that records overdue approval clocks as anomalies rather than locks.</summary>
public sealed class HoursServerApprovalDeadlineScheduler : BackgroundService
{
    private readonly IHoursJournal journal;
    private readonly IWorkflowEngine workflows;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<HoursServerApprovalDeadlineScheduler> logger;

    /// <summary>Initializes the approval-deadline scheduler.</summary>
    public HoursServerApprovalDeadlineScheduler(
        IHoursJournal journal,
        IWorkflowEngine workflows,
        TimeProvider timeProvider,
        ILogger<HoursServerApprovalDeadlineScheduler> logger)
    {
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
        this.workflows = workflows ?? throw new ArgumentNullException(nameof(workflows));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await AssessAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(24), timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task AssessAsync(CancellationToken cancellationToken)
    {
        try
        {
            var entries = await journal.ReadAllAsync(cancellationToken);
            var asOf = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            foreach (var employeeId in entries.Select(item => item.EmployeeId).Distinct(StringComparer.Ordinal))
            {
                var view = HoursProjector.Replay(
                    employeeId,
                    entries.Where(item => item.EmployeeId == employeeId));
                foreach (var period in view.ApprovalPeriods.Where(period =>
                    period.State is HoursApprovalState.Registered or HoursApprovalState.EmployeeSubmitted or HoursApprovalState.Escalated))
                {
                    var result = await workflows.ExecuteAsync(
                        "hours.approval-deadline",
                        new HoursServerApprovalDeadlineCheck(period.Id, asOf, HoursActor.System()),
                        cancellationToken);
                    if (result.Status != WorkflowStatus.Succeeded)
                    {
                        logger.LogWarning(
                            result.Error,
                            "Hours deadline workflow failed for approval period {ApprovalPeriodId}.",
                            period.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Hours deadline scheduler failed its non-blocking assessment.");
        }
    }
}
