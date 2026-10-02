namespace Novolis.Ndjson;

public sealed record NdjsonSlice(
    long Skip,
    int Take,
    IReadOnlyList<NdjsonRecord> Records,
    bool HasPrevious,
    bool HasMore);
