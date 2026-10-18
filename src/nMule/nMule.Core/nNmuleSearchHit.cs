using Novolis.Transports.Ed2k;

namespace Novolis.NMule.Core;

/// <summary>UI-safe projection of one eD2K or Kad search result.</summary>
public sealed record NmuleSearchHit(Ed2kSearchResult Result)
{
    /// <summary>Gets the advertised file name.</summary>
    public string Name => Result.Name;

    /// <summary>Gets the advertised byte count.</summary>
    public long Size => Result.Size;

    /// <summary>Gets the eD2K file identifier.</summary>
    public Ed2kFileId FileId => Result.FileId;

    /// <summary>Gets the advertised source count.</summary>
    public int SourceCount => Result.SourceCount;

    /// <summary>Gets the transfer path available without callback hosting.</summary>
    public NmuleTransferEligibility Eligibility
    {
        get
        {
            if (Size <= 0)
                return NmuleTransferEligibility.InvalidSource;
            if (Size > Ed2kProtocol.PartSize)
                return NmuleTransferEligibility.MultipartRequired;
            return Ed2kMessages.IsHighId(Result.ClientId)
                ? NmuleTransferEligibility.DirectHighId
                : NmuleTransferEligibility.KadSourceRequired;
        }
    }

    /// <summary>Gets a display-ready eligibility explanation.</summary>
    public string EligibilityLabel => Eligibility switch
    {
        NmuleTransferEligibility.DirectHighId => "Direct download",
        NmuleTransferEligibility.KadSourceRequired => "Kad source lookup",
        NmuleTransferEligibility.MultipartRequired => "Multipart required",
        NmuleTransferEligibility.InvalidSource => "Unavailable",
        _ => "Callback required",
    };
}
