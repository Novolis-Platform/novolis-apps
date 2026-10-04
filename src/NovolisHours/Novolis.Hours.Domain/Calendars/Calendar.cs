using System.Collections.Immutable;
using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>One effective-dated, versioned layer in an ordered calendar stack.</summary>
public sealed record Calendar
{
    /// <summary>Initializes a calendar layer.</summary>
    public Calendar(
        string id,
        string version,
        int order,
        IEnumerable<ICalendarRule> rules,
        RuleSource source,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(rules);
        if (effectiveTo < effectiveFrom)
        {
            throw new ArgumentException(
                "The effective end date must not precede the effective start date.",
                nameof(effectiveTo));
        }

        Id = id;
        Version = version;
        Order = order;
        Rules = rules.ToImmutableArray();
        Source = source;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    /// <summary>Stable layer identity.</summary>
    public string Id { get; }

    /// <summary>Configuration version for this layer.</summary>
    public string Version { get; }

    /// <summary>Explicit precedence; higher values are applied later.</summary>
    public int Order { get; }

    /// <summary>Selectors and their semantic contributions.</summary>
    public ImmutableArray<ICalendarRule> Rules { get; }

    /// <summary>Origin of the layer.</summary>
    public RuleSource Source { get; }

    /// <summary>Inclusive effective start, when constrained.</summary>
    public DateOnly? EffectiveFrom { get; }

    /// <summary>Inclusive effective end, when constrained.</summary>
    public DateOnly? EffectiveTo { get; }

    /// <summary>Whether the layer participates on a local date.</summary>
    public bool AppliesTo(DateOnly date) =>
        (!EffectiveFrom.HasValue || date >= EffectiveFrom.Value) &&
        (!EffectiveTo.HasValue || date <= EffectiveTo.Value);
}
