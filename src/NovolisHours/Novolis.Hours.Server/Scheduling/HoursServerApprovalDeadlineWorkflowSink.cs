using Microsoft.Extensions.Logging;
using Novolis.Hours.Domain;
using Novolis.WorkflowEngine;

namespace Novolis.Hours.Server;

/// <summary>Logs completion of deadline assessment workflows without changing their domain outcome.</summary>
public sealed class HoursServerApprovalDeadlineWorkflowSink : IWorkflowSink<HoursApprovalPeriod>
{
    private readonly ILogger<HoursServerApprovalDeadlineWorkflowSink> logger;

    /// <summary>Initializes the workflow sink.</summary>
    public HoursServerApprovalDeadlineWorkflowSink(ILogger<HoursServerApprovalDeadlineWorkflowSink> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(
        HoursApprovalPeriod payload,
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Hours approval period {ApprovalPeriodId} assessed by workflow {WorkflowName} in state {ApprovalState}.",
            payload.Id,
            context.WorkflowName,
            payload.State);
        return ValueTask.CompletedTask;
    }
}
