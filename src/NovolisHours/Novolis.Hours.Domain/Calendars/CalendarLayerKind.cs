namespace Novolis.Hours.Domain.Calendars;

/// <summary>Named position of a calendar layer in the standard sparse stack.</summary>
public enum CalendarLayerKind
{
    /// <summary>Layer kind was not recorded.</summary>
    Unspecified = 0,

    /// <summary>National weekend and public-holiday contributions.</summary>
    National = 100,

    /// <summary>Company-wide working pattern and paid-day observances.</summary>
    Organisation = 200,

    /// <summary>Collective-agreement contributions.</summary>
    Agreement = 300,

    /// <summary>Employment expected work, envelope, and core hours.</summary>
    Employment = 400,

    /// <summary>Person-specific routine.</summary>
    Employee = 500,

    /// <summary>Short-lived exclusive override.</summary>
    TemporaryOverride = 600,
}
