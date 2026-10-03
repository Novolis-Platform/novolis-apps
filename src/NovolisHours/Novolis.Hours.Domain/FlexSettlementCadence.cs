namespace Novolis.Hours.Domain;

/// <summary>Defines when a configured flex carry policy is assessed.</summary>
public enum FlexSettlementCadence
{
    /// <summary>Assessment is initiated explicitly by an authorised user.</summary>
    Manual,

    /// <summary>Assessment closes at the end of each calendar month.</summary>
    Monthly,

    /// <summary>Assessment closes at the end of each calendar quarter.</summary>
    Quarterly,

    /// <summary>Assessment closes at the end of each calendar year.</summary>
    Annual,
}
