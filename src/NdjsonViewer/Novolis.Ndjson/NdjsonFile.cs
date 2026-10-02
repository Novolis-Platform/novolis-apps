namespace Novolis.Ndjson;

public sealed class NdjsonFile(FileInfo file)
{
    public FileInfo File { get; } = file ?? throw new ArgumentNullException(nameof(file));
}
