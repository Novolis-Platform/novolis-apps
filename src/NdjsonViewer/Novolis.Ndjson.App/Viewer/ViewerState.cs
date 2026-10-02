using Novolis.Ndjson;

namespace Novolis.Ndjson.App.Viewer;

public sealed record ViewerState(
    long Skip,
    int Take,
    NdjsonSlice? Slice,
    bool IsRefreshing,
    Exception? Error);
