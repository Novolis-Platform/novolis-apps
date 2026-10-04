using System.Collections.Immutable;

namespace Novolis.Hours.Domain.Dimensions;

/// <summary>Resolved sparse Dimension definitions and deterministic rule order.</summary>
public sealed class DimensionConfiguration
{
    private readonly ImmutableArray<DimensionDefinition> definitions;
    private readonly ImmutableArray<DimensionRuleRegistration> rules;

    /// <summary>Initializes and resolves ordered configuration layers.</summary>
    public DimensionConfiguration(IEnumerable<DimensionConfigurationLayer> layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        Layers = layers
            .OrderBy(layer => layer.Order)
            .ThenBy(layer => layer.Id, StringComparer.Ordinal)
            .ToImmutableArray();

        var definitionMap = new Dictionary<string, DimensionDefinition>(
            StringComparer.Ordinal);
        var ruleMap = new Dictionary<string, DimensionRuleRegistration>(
            StringComparer.Ordinal);
        foreach (var layer in Layers)
        {
            foreach (var definition in layer.Definitions)
            {
                definitionMap[definition.Id] = definition;
            }

            foreach (var rule in layer.Rules.OrderBy(rule => rule.Order))
            {
                ruleMap[rule.Id] = rule;
            }
        }

        definitions = definitionMap.Values
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        rules = ruleMap.Values
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>Ordered source layers retained for audit.</summary>
    public ImmutableArray<DimensionConfigurationLayer> Layers { get; }

    /// <summary>Resolved definitions.</summary>
    public ImmutableArray<DimensionDefinition> Definitions => definitions;

    /// <summary>Resolved ordered rules.</summary>
    public ImmutableArray<DimensionRuleRegistration> Rules => rules;

    /// <summary>Finds a definition by stable ID.</summary>
    public DimensionDefinition? GetDefinition(string id) =>
        definitions.FirstOrDefault(
            definition => definition.Id.Equals(id, StringComparison.Ordinal));
}
