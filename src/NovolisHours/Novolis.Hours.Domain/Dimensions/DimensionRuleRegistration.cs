using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Ordered typed rule plus stable provenance metadata.</summary>
public sealed record DimensionRuleRegistration
{
    /// <summary>Initializes a rule registration.</summary>
    public DimensionRuleRegistration(
        string id,
        int order,
        RuleRef provenance,
        IDimensionRule rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(rule);
        Id = id;
        Order = order;
        Provenance = provenance;
        Rule = rule;
    }

    /// <summary>Stable registration identity.</summary>
    public string Id { get; }

    /// <summary>Order within its configuration layer.</summary>
    public int Order { get; }

    /// <summary>Rule provenance attached to every derived result.</summary>
    public RuleRef Provenance { get; }

    /// <summary>Typed implementation.</summary>
    public IDimensionRule Rule { get; }
}
