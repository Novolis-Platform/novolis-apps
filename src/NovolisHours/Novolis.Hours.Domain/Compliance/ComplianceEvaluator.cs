using System.Collections.Immutable;
using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Compliance;

/// <summary>Runs reporting rules after registration; it has no rejection or workflow dependency.</summary>
public sealed class ComplianceEvaluator
{
    /// <summary>Evaluates all ordered rules and returns factual indicators.</summary>
    public ComplianceEvaluation Evaluate(
        ComplianceContext context,
        IEnumerable<ComplianceRuleRegistration> rules)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rules);
        var indicators = ImmutableArray.CreateBuilder<ComplianceIndicator>();
        foreach (var registration in rules.OrderBy(rule => rule.Order).ThenBy(rule => rule.Id))
        {
            var produced = registration.Rule.Evaluate(context)
                ?? throw new InvalidOperationException(
                    $"Compliance rule '{registration.Id}' returned no result collection.");
            foreach (var indicator in produced)
            {
                indicators.Add(EnsureProvenance(indicator, registration.Provenance));
            }
        }

        return new ComplianceEvaluation(context.WorkDay, indicators);
    }

    private static ComplianceIndicator EnsureProvenance(
        ComplianceIndicator indicator,
        RuleRef fallback)
    {
        ArgumentNullException.ThrowIfNull(indicator);
        return indicator.Rule is null
            ? new ComplianceIndicator(
                indicator.Code,
                indicator.Category,
                indicator.Message,
                indicator.WorkDay,
                indicator.AffectedInterval,
                fallback)
            : indicator;
    }
}
