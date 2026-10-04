namespace Novolis.Hours.Domain.Compliance;

/// <summary>Typed reporting rule that observes resolved work without changing it.</summary>
public interface IComplianceRule
{
    /// <summary>Returns zero or more factual indicators.</summary>
    IReadOnlyList<ComplianceIndicator> Evaluate(ComplianceContext context);
}
