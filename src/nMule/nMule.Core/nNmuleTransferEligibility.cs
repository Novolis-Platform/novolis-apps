namespace Novolis.NMule.Core;

/// <summary>Why a search result can or cannot be transferred by the MVP.</summary>
public enum NmuleTransferEligibility
{
    DirectHighId,
    KadSourceRequired,
    LowIdRequiresCallback,
    InvalidSource,
}
