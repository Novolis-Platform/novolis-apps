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

    /// <summary>Gets the advertised file type.</summary>
    public string TypeLabel =>
        string.IsNullOrWhiteSpace(Result.Type) ? "Unknown type" : Result.Type;

    /// <summary>Gets a compact size/type/availability summary.</summary>
    public string Summary =>
        $"{Size:N0} bytes · {TypeLabel} · {AvailabilityLabel}";

    /// <summary>
    /// Gets the server-advertised availability. It is not a live reachability
    /// check.
    /// </summary>
    public string AvailabilityLabel =>
        SourceCount > 0
            ? $"{SourceCount:N0} advertised source(s)"
            : "No advertised sources";

    /// <summary>Gets the eD2K client identifier from the search result.</summary>
    public string ClientIdLabel => $"0x{Result.ClientId:X8} ({Result.ClientId})";

    /// <summary>Gets the source port advertised by the search result.</summary>
    public string PortLabel =>
        Result.Port is > 0
            ? Result.Port.ToString()
            : "Not advertised";

    /// <summary>Gets the full metadata shown by the expandable result details.</summary>
    public string DetailsText =>
        $"Type: {TypeLabel}{Environment.NewLine}"
        + $"Size: {Size:N0} bytes{Environment.NewLine}"
        + $"Availability: {AvailabilityLabel}; live reachability not verified"
        + $"{Environment.NewLine}"
        + $"Transfer path: {EligibilityLabel}{Environment.NewLine}"
        + $"File ID: {FileId}{Environment.NewLine}"
        + $"Client ID: {ClientIdLabel}{Environment.NewLine}"
        + $"Advertised source port: {PortLabel}";

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
