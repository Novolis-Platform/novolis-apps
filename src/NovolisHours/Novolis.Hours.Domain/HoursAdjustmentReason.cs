namespace Novolis.Hours.Domain;

/// <summary>Explains a proposed adjustment without creating a separate balance account.</summary>
public enum HoursAdjustmentReason
{
    /// <summary>Presence is being reclassified as financially compensated time.</summary>
    ConvertToFinancialCompensation,

    /// <summary>A break or non-working activity should not count as worked time.</summary>
    NotOnClock,

    /// <summary>A factual correction to a previous worktime record.</summary>
    Correction,

    /// <summary>A configured carry policy proposes normalization of unused positive flex; no payment is implied.</summary>
    FlexNormalization,

    /// <summary>An organisation-specific reason described in the attached comment.</summary>
    Other,
}
