using Novolis.Hours.Domain.Calendars;
using Novolis.Hours.Domain.Configuration;
using Novolis.Hours.Domain.Work;

namespace Novolis.Hours.Domain.Compliance;

/// <summary>Neutral factual concern produced after work registration.</summary>
public sealed record ComplianceIndicator
{
    /// <summary>Initializes an indicator.</summary>
    public ComplianceIndicator(
        string code,
        string category,
        string message,
        WorkDayKey workDay,
        WorkInterval? affectedInterval,
        RuleRef rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(rule);
        Code = code;
        Category = category;
        Message = message;
        WorkDay = workDay;
        AffectedInterval = affectedInterval;
        Rule = rule;
    }

    /// <summary>Stable indicator code.</summary>
    public string Code { get; }

    /// <summary>Neutral grouping category.</summary>
    public string Category { get; }

    /// <summary>Factual explanation without a motive or guilt claim.</summary>
    public string Message { get; }

    /// <summary>Logical workday that produced the indicator.</summary>
    public WorkDayKey WorkDay { get; }

    /// <summary>Optional affected work interval.</summary>
    public WorkInterval? AffectedInterval { get; }

    /// <summary>Rule provenance.</summary>
    public RuleRef Rule { get; }
}
