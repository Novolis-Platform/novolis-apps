using System.Security.Cryptography;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostFileTransfers(ReachHostRuntime runtime)
{
    internal async Task HandleOfferAsync(
        ReachHostClientConnection connection,
        ReachFileOffer offer,
        CancellationToken cancellationToken)
    {
        const long maximumLength = 2L * 1024 * 1024 * 1024;
        if (offer.Length < 0 || offer.Length > maximumLength)
            throw new InvalidDataException("Reach file offer exceeds the host limit.");

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "Reach",
            "Incoming");
        Directory.CreateDirectory(directory);
        var safeName = Path.GetFileName(offer.Name);
        if (string.IsNullOrWhiteSpace(safeName))
            throw new InvalidDataException("Reach file offer has no file name.");

        var path = Path.Combine(directory, $"{offer.TransferId:N}-{safeName}");
        var transfer = new ReachHostFileTransfer(
            connection.Id,
            offer.TransferId,
            path,
            offer.Length,
            offer.Hash,
            new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan));
        if (!runtime.FileTransfers.TryAdd(offer.TransferId, transfer))
        {
            await transfer.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Reach transfer id was already active.");
        }

        runtime.WriteLog(
            $"Receiving {safeName} ({offer.Length} bytes) from client {connection.Id}.");
        await connection.SendAsync(
                ReachMessageType.FileOffer,
                offer,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task HandleChunkAsync(
        ReachFileChunk chunk,
        CancellationToken cancellationToken)
    {
        if (!runtime.FileTransfers.TryGetValue(chunk.TransferId, out var transfer))
            throw new InvalidDataException("Reach file chunk has no active offer.");
        if (chunk.Offset != transfer.BytesWritten)
            throw new InvalidDataException("Reach file chunk offset is not sequential.");
        if (chunk.Data.Length > 1024 * 1024
            || transfer.BytesWritten + chunk.Data.Length > transfer.ExpectedLength)
        {
            throw new InvalidDataException("Reach file chunk exceeds its offer.");
        }

        await transfer.Stream.WriteAsync(chunk.Data, cancellationToken)
            .ConfigureAwait(false);
        transfer.BytesWritten += chunk.Data.Length;
    }

    internal async Task HandleCompleteAsync(
        ReachHostClientConnection connection,
        ReachFileComplete complete,
        CancellationToken cancellationToken)
    {
        if (!runtime.FileTransfers.TryRemove(complete.TransferId, out var transfer))
            throw new InvalidDataException("Reach file completion has no active offer.");

        var success = complete.Succeeded
            && transfer.BytesWritten == transfer.ExpectedLength;
        await transfer.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        await transfer.DisposeAsync().ConfigureAwait(false);
        if (success)
        {
            await using var receivedFile = File.OpenRead(transfer.Path);
            var actualHash = Convert.ToHexString(
                await SHA256.HashDataAsync(receivedFile, cancellationToken)
                    .ConfigureAwait(false));
            success = string.Equals(
                actualHash,
                transfer.ExpectedHash,
                StringComparison.OrdinalIgnoreCase);
        }

        runtime.WriteLog(success
            ? $"Received file {transfer.Path}."
            : $"Rejected incomplete or invalid file transfer {complete.TransferId}.");
        await connection.SendAsync(
                ReachMessageType.FileComplete,
                complete with
                {
                    Succeeded = success,
                    Error = success ? null : "Host validation failed.",
                },
                cancellationToken)
            .ConfigureAwait(false);
    }
}
