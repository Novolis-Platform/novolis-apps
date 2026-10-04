using System.Security.Cryptography;
using Novolis.Transports;

namespace Novolis.Reach.Client;

internal sealed class ReachClientFileSender(ReachClientSession session)
{
    internal async Task SendAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
            throw new FileNotFoundException("The file to transfer does not exist.", path);

        var hash = await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false);
        var transferId = Guid.NewGuid();
        var bulkStream = session._transport is { Info.Kind: TransportKind.Quic }
            ? await session._transport.OpenBulkStreamAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;
        var stream = bulkStream ?? session._stream
            ?? throw new InvalidOperationException("Reach is not connected.");
        var gate = bulkStream is null ? session._sendGate : session._bulkSendGate;
        try
        {
            if (bulkStream is not null)
            {
                await ReachClientIO.SendToStreamAsync(
                        session,
                        stream,
                        gate,
                        ReachMessageType.BulkHello,
                        new ReachBulkHello(
                            session._sessionId,
                            ReachProtocol.AppId,
                            ReachProtocol.Version),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await ReachClientIO.SendToStreamAsync(
                    session,
                    stream,
                    gate,
                    ReachMessageType.FileOffer,
                    new ReachFileOffer(
                        transferId,
                        Path.GetFileName(path),
                        fileInfo.Length,
                        Convert.ToHexString(hash)),
                    cancellationToken)
                .ConfigureAwait(false);

            await using var file = File.OpenRead(path);
            var buffer = new byte[64 * 1024];
            long offset = 0;
            while (true)
            {
                var read = await file.ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    break;

                await ReachClientIO.SendToStreamAsync(
                        session,
                        stream,
                        gate,
                        ReachMessageType.FileChunk,
                        new ReachFileChunk(
                            transferId,
                            offset,
                            buffer.AsSpan(0, read).ToArray()),
                        cancellationToken)
                    .ConfigureAwait(false);
                offset += read;
            }

            await ReachClientIO.SendToStreamAsync(
                    session,
                    stream,
                    gate,
                    ReachMessageType.FileComplete,
                    new ReachFileComplete(transferId, true, null),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            bulkStream?.Dispose();
        }
    }

    private static async Task<byte[]> ComputeHashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
                break;
            hash.AppendData(buffer, 0, read);
        }

        return hash.GetHashAndReset();
    }
}
