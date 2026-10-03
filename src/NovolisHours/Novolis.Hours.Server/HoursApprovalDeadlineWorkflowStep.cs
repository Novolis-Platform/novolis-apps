using Novolis.Hours.Domain;
using Novolis.WorkflowEngine;

namespace Novolis.Hours.Server;

/// <summary>Workflow step that writes deadline anomalies through the same append-only application service.</summary>
public sealed class HoursApprovalDeadlineWorkflowStep : IWorkflowStep<HoursApprovalDeadlineCheck, HoursApprovalPeriod>
{
    private readonly HoursService hours;

    /// <summary>Initializes the deadline workflow step.</summary>
    public HoursApprovalDeadlineWorkflowStep(HoursService hours)
    {
        this.hours = hours ?? throw new ArgumentNullException(nameof(hours));
    }

    /// <inheritdoc />
    public ValueTask<HoursApprovalPeriod> ExecuteAsync(
        HoursApprovalDeadlineCheck input,
        WorkflowContext context,
        CancellationToken cancellationToken = default) =>
        hours.CheckApprovalDeadlinesAsync(input.PeriodId, input.AsOf, input.Actor, cancellationToken);
}
