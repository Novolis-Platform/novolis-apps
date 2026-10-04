using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Review;

/// <summary>Projects review actions, deadlines, disputes, and post-approval changes.</summary>
public sealed class ReviewProjector
{
    /// <summary>Builds a transparent projection as of a local date.</summary>
    public ReviewProjection Project(
        ReviewPeriod period,
        ReviewPolicy policy,
        IEnumerable<ReviewAction> actions,
        DateOnly asOf,
        DateTimeOffset? latestFactAt = null)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(actions);
        var relevantActions = actions
            .Where(action => action.PeriodId == period.Id)
            .OrderBy(action => action.RecordedAt)
            .ThenBy(action => action.Id)
            .ToImmutableArray();
        var latestApproval = relevantActions
            .Where(action => action.Kind is ReviewActionKind.Approve or ReviewActionKind.Acknowledge)
            .Select(action => (DateTimeOffset?)action.RecordedAt)
            .Max();
        var changedAfterApproval = latestFactAt.HasValue &&
            latestApproval.HasValue &&
            latestFactAt.Value > latestApproval.Value;
        var correctionCutoff = policy.ReopenAfterCorrection && changedAfterApproval
            ? latestFactAt
            : null;

        var stageResults = ImmutableArray.CreateBuilder<ReviewStageResult>();
        var anomalies = ImmutableArray.CreateBuilder<ReviewAnomaly>();
        var cursor = period.Through;
        foreach (var stage in policy.Stages)
        {
            var dueDate = BusinessDayCalculator.AddBusinessDays(
                cursor,
                stage.BusinessDays);
            cursor = dueDate;
            var satisfied = relevantActions
                .Where(action => action.Kind == stage.RequiredAction)
                .Where(action => MatchesRole(action, stage.Role))
                .Where(action => !stage.ApprovalLevel.HasValue ||
                    action.ApprovalLevel == stage.ApprovalLevel)
                .Where(action => !correctionCutoff.HasValue ||
                    action.RecordedAt >= correctionCutoff.Value)
                .OrderBy(action => action.RecordedAt)
                .ThenBy(action => action.Id)
                .FirstOrDefault();
            var overdue = satisfied is null && asOf > dueDate;
            var result = new ReviewStageResult(
                stage,
                dueDate,
                satisfied is not null,
                satisfied,
                overdue);
            stageResults.Add(result);
            if (overdue)
            {
                anomalies.Add(new ReviewAnomaly(
                    $"review.{stage.Id}.overdue",
                    $"The {stage.Id} review stage is overdue; the period remains open for transparent follow-up.",
                    stage.Id,
                    dueDate));
            }
        }

        var unresolvedDispute = FindUnresolvedDispute(relevantActions);
        var escalations = unresolvedDispute is null
            ? ImmutableArray<ReviewEscalation>.Empty
            : ImmutableArray.Create(
                new ReviewEscalation(
                    unresolvedDispute.Id,
                    policy.DisputeTarget,
                    unresolvedDispute.Comment!));
        var state = unresolvedDispute is not null
            ? ReviewState.Disputed
            : stageResults.All(stage => stage.IsComplete)
                ? ReviewState.Approved
                : relevantActions.Any(action => action.Kind == ReviewActionKind.Submit)
                    ? ReviewState.Submitted
                    : ReviewState.Registered;

        return new ReviewProjection(
            period,
            policy,
            relevantActions,
            stageResults,
            anomalies,
            escalations,
            state,
            changedAfterApproval);
    }

    private static bool MatchesRole(
        ReviewAction action,
        ResponsibilityRole role) =>
        action.Actor.Role.Equals(
            role.ToString(),
            StringComparison.OrdinalIgnoreCase);

    private static ReviewAction? FindUnresolvedDispute(
        ImmutableArray<ReviewAction> actions)
    {
        ReviewAction? unresolved = null;
        foreach (var action in actions)
        {
            if (action.Kind == ReviewActionKind.Dispute)
            {
                unresolved = action;
            }
            else if (action.Kind == ReviewActionKind.Resolve)
            {
                unresolved = null;
            }
        }

        return unresolved;
    }
}
