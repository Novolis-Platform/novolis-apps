namespace Novolis.Ndjson.App;

public sealed record NdjsonOpenRequest(
    string DisplayName,
    FileInfo? PhysicalFile,
    NdjsonStreamFactory? OpenReadAsync)
{
    public static NdjsonOpenRequest FromFile(FileInfo file) =>
        new(Path.GetFileName(file.FullName), file, null);

    public static NdjsonOpenRequest FromStream(
        string displayName,
        NdjsonStreamFactory openReadAsync) =>
        new(displayName, null, openReadAsync);
}
