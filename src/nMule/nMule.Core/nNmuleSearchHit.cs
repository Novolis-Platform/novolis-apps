using Novolis.Transports.Ed2k;

namespace Novolis.NMule.Core;

/// <summary>UI-safe projection of one eD2K or Kad search result.</summary>
public sealed record NmuleSearchHit(
    Ed2kSearchResult Result,
    IReadOnlyList<string>? SameHashNames = null,
    string? OutputDirectory = null)
{
    /// <summary>Gets the advertised file name.</summary>
    public string Name => Result.Name;

    /// <summary>Gets the advertised byte count.</summary>
    public long Size => Result.Size;

    /// <summary>Gets the eD2K file identifier.</summary>
    public Ed2kFileId FileId => Result.FileId;

    /// <summary>Gets the advertised source count.</summary>
    public int SourceCount => Result.SourceCount;

    /// <summary>Gets the advertised complete-source count, when supplied.</summary>
    public int? CompleteSourceCount => Result.CompleteSourceCount;

    /// <summary>Gets the advertised source speed, when supplied.</summary>
    public long? SpeedBytesPerSecond => Result.SpeedBytesPerSecond;

    /// <summary>Gets the advertised availability percentage, when supplied.</summary>
    public int? AvailabilityPercent => Result.AvailabilityPercent;

    /// <summary>Gets the eD2K hash shown in the result grid.</summary>
    public string HashLabel => FileId.ToString();

    /// <summary>Gets the advertised file type.</summary>
    public string TypeLabel =>
        string.IsNullOrWhiteSpace(Result.Type) ? "Unknown type" : Result.Type;

    /// <summary>Gets all names advertised for this file hash in this search.</summary>
    public IReadOnlyList<string> NameVariants
    {
        get
        {
            var names = SameHashNames?
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return names is { Length: > 0 } ? names : [Name];
        }
    }

    /// <summary>Gets a compact size/type/availability summary.</summary>
    public string Summary =>
        $"{Size:N0} bytes · {TypeLabel} · {AvailabilityLabel} · {SpeedLabel}";

    /// <summary>
    /// Gets the server-advertised availability. It is not a live reachability
    /// check.
    /// </summary>
    public string AvailabilityLabel =>
        SourceCount <= 0
            ? "No advertised sources"
            : AvailabilityPercent is >= 0 and <= 100
                ? $"{SourceCount:N0} source(s) · {AvailabilityPercent}% availability"
                : CompleteSourceCount is > 0
                    ? $"{SourceCount:N0} source(s) · {CompleteSourceCount:N0} complete"
                    : $"{SourceCount:N0} advertised source(s)";

    /// <summary>Gets the server-advertised source speed, when available.</summary>
    public string SpeedLabel =>
        SpeedBytesPerSecond is > 0
            ? FormatRate(SpeedBytesPerSecond.Value)
            : "Speed not advertised";

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
        + $"Speed: {SpeedLabel}{Environment.NewLine}"
        + $"Hash: {HashLabel}{Environment.NewLine}"
        + $"Names for this hash: {string.Join(", ", NameVariants)}{Environment.NewLine}"
        + $"Transfer path: {EligibilityLabel}{Environment.NewLine}"
        + $"Client ID: {ClientIdLabel}{Environment.NewLine}"
        + $"Advertised source port: {PortLabel}{Environment.NewLine}"
        + $"Output location: {OutputLocationLabel}";

    /// <summary>Gets the names grouped under this file hash.</summary>
    public string SameHashNamesLabel =>
        NameVariants.Count > 1
            ? $"{NameVariants.Count:N0} names for this hash: "
                + string.Join(", ", NameVariants)
            : "No alternate names for this hash";

    /// <summary>Gets the app-private destination for this result.</summary>
    public string OutputLocationLabel =>
        string.IsNullOrWhiteSpace(OutputDirectory)
            ? "App-private nMule output"
            : OutputDirectory;

    /// <summary>Gets the transfer path available without callback hosting.</summary>
    public NmuleTransferEligibility Eligibility
    {
        get
        {
            if (Size <= 0 || Size > uint.MaxValue)
                return NmuleTransferEligibility.InvalidSource;
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
        NmuleTransferEligibility.InvalidSource => "Unavailable",
        _ => "Callback required",
    };

    private static string FormatRate(long bytesPerSecond)
    {
        const double Kibi = 1024;
        const double Mebi = Kibi * 1024;
        const double Gibi = Mebi * 1024;
        return bytesPerSecond switch
        {
            >= (long)Gibi => $"{bytesPerSecond / Gibi:0.##} GiB/s",
            >= (long)Mebi => $"{bytesPerSecond / Mebi:0.##} MiB/s",
            >= (long)Kibi => $"{bytesPerSecond / Kibi:0.##} KiB/s",
            _ => $"{bytesPerSecond:N0} B/s",
        };
    }
}
