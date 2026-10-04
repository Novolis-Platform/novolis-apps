using System.Collections.Immutable;
using Novolis.Hours.Domain.Configuration;

namespace Novolis.Hours.Domain.Calendars;

/// <summary>One effective-dated, versioned layer in an ordered work-calendar stack.</summary>
public sealed record WorkCalendarLayer
{
    /// <summary>Initializes a work-calendar layer.</summary>
    public WorkCalendarLayer(
        string id,
        string version,
        int order,
        IEnumerable<IWorkCalendarRule> rules,
        RuleSource source,
        DateOnly? effectiveFrom = null,
        DateOnly? effectiveTo = null,
        CalendarLayerKind kind = CalendarLayerKind.Unspecified)
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
        Kind = kind == CalendarLayerKind.Unspecified ? InferKind(order) : kind;
    }

    /// <summary>Stable layer identity.</summary>
    public string Id { get; }

    /// <summary>Configuration version for this layer.</summary>
    public string Version { get; }

    /// <summary>Explicit precedence; higher values are applied later.</summary>
    public int Order { get; }

    /// <summary>Selectors and their semantic contributions.</summary>
    public ImmutableArray<IWorkCalendarRule> Rules { get; }

    /// <summary>Origin of the layer.</summary>
    public RuleSource Source { get; }

    /// <summary>Inclusive effective start, when constrained.</summary>
    public DateOnly? EffectiveFrom { get; }

    /// <summary>Inclusive effective end, when constrained.</summary>
    public DateOnly? EffectiveTo { get; }

    /// <summary>Standard stack position of this layer.</summary>
    public CalendarLayerKind Kind { get; }

    /// <summary>Whether the layer participates on a local date.</summary>
    public bool AppliesTo(DateOnly date) =>
        (!EffectiveFrom.HasValue || date >= EffectiveFrom.Value) &&
        (!EffectiveTo.HasValue || date <= EffectiveTo.Value);

    private static CalendarLayerKind InferKind(int order) =>
        order switch
        {
            CalendarLayerOrders.National => CalendarLayerKind.National,
            CalendarLayerOrders.Organisation => CalendarLayerKind.Organisation,
            CalendarLayerOrders.Agreement => CalendarLayerKind.Agreement,
            CalendarLayerOrders.Employment => CalendarLayerKind.Employment,
            CalendarLayerOrders.Employee => CalendarLayerKind.Employee,
            CalendarLayerOrders.TemporaryOverride => CalendarLayerKind.TemporaryOverride,
            _ => CalendarLayerKind.Unspecified,
        };
}
