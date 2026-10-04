using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Compliance;

/// <summary>Ordered compliance rule and its retained provenance.</summary>
public sealed record ComplianceRuleRegistration
{
    /// <summary>Initializes a registration with invariant checks.</summary>
    public ComplianceRuleRegistration(
        string id,
        int order,
        RuleRef provenance,
        IComplianceRule rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(rule);
        Id = id;
        Order = order;
        Provenance = provenance;
        Rule = rule;
    }

    /// <summary>Stable rule identity.</summary>
    public string Id { get; }

    /// <summary>Deterministic evaluation order.</summary>
    public int Order { get; }

    /// <summary>Rule provenance.</summary>
    public RuleRef Provenance { get; }

    /// <summary>Typed rule implementation.</summary>
    public IComplianceRule Rule { get; }
}
