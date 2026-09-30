using System.Text.Json.Serialization;
using Novolis.Math.Geometry;

namespace PresenceLedger.Core;

/// <summary>Operational state of one configured location.</summary>
public enum PresenceState
{
    /// <summary>No confirmed presence and no active arrival candidate.</summary>
    Absent,

    /// <summary>Qualifying evidence is accumulating for arrival.</summary>
    CandidatePresent,

    /// <summary>Arrival has been confirmed.</summary>
    Present,

    /// <summary>Evidence is accumulating for departure.</summary>
    CandidateAbsent,
}
