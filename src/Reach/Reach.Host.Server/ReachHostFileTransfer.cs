namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostFileTransfer : IAsyncDisposable
{
    internal ReachHostFileTransfer(
        long clientId,
        Guid transferId,
        string path,
        long expectedLength,
        string expectedHash,
        FileStream stream)
    {
        ClientId = clientId;
        TransferId = transferId;
        Path = path;
        ExpectedLength = expectedLength;
        ExpectedHash = expectedHash;
        Stream = stream;
    }

    internal long ClientId { get; }
    internal Guid TransferId { get; }
    internal string Path { get; }
    internal long ExpectedLength { get; }
    internal string ExpectedHash { get; }
    internal FileStream Stream { get; }
    internal long BytesWritten { get; set; }

    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}
