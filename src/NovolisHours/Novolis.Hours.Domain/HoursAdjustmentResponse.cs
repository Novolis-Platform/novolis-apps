namespace Novolis.Hours.Domain;

/// <summary>Employee response to a proposed worktime adjustment.</summary>
public enum HoursAdjustmentResponse
{
    /// <summary>The employee approves the proposed duration movement.</summary>
    Accept,

    /// <summary>The employee requests HR or Higher review while preserving the proposed record.</summary>
    Dispute,
}
