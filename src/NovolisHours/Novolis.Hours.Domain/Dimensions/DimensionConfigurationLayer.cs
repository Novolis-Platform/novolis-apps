using System.Collections.Immutable;
using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>One sparse, ordered Dimension configuration contribution.</summary>
public sealed record DimensionConfigurationLayer
{
    /// <summary>Initializes a Dimension configuration layer.</summary>
    public DimensionConfigurationLayer(
        string id,
        string version,
        int order,
        IEnumerable<DimensionDefinition> definitions,
        IEnumerable<DimensionRuleRegistration> rules,
        RuleSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(rules);
        Id = id;
        Version = version;
        Order = order;
        Definitions = definitions.ToImmutableArray();
        Rules = rules.ToImmutableArray();
        Source = source;
    }

    /// <summary>Stable layer identity.</summary>
    public string Id { get; }

    /// <summary>Layer configuration version.</summary>
    public string Version { get; }

    /// <summary>Explicit precedence.</summary>
    public int Order { get; }

    /// <summary>Sparse definitions supplied by the layer.</summary>
    public ImmutableArray<DimensionDefinition> Definitions { get; }

    /// <summary>Rules supplied by the layer.</summary>
    public ImmutableArray<DimensionRuleRegistration> Rules { get; }

    /// <summary>Source category.</summary>
    public RuleSource Source { get; }
}
