namespace Novolis.Hours.Domain.Calendars;

/// <summary>Conventional order values for the standard calendar stack.</summary>
public static class CalendarLayerOrders
{
    /// <summary>National layer precedence.</summary>
    public const int National = (int)CalendarLayerKind.National;

    /// <summary>Organisation layer precedence.</summary>
    public const int Organisation = (int)CalendarLayerKind.Organisation;

    /// <summary>Agreement layer precedence.</summary>
    public const int Agreement = (int)CalendarLayerKind.Agreement;

    /// <summary>Employment layer precedence.</summary>
    public const int Employment = (int)CalendarLayerKind.Employment;

    /// <summary>Employee layer precedence.</summary>
    public const int Employee = (int)CalendarLayerKind.Employee;

    /// <summary>Temporary override layer precedence.</summary>
    public const int TemporaryOverride = (int)CalendarLayerKind.TemporaryOverride;
}
