using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Novolis.Transports;
using Novolis.Transports.Quic;
using Novolis.Transports.Tcp;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostControlListeners(ReachHostRuntime runtime)
{
    internal async Task RunTcpAsync(
        IPAddress address,
        CancellationToken cancellationToken)
    {
        TcpTransportListener listener;
        try
        {
            listener = new TcpTransportListener(
                new IPEndPoint(address, ReachProtocol.ControlPort));
        }
        catch (SocketException exception)
        {
            runtime.WriteLog(
                $"Could not listen for Reach clients on {address}:{ReachProtocol.ControlPort}: {exception.Message}");
            return;
        }

        runtime.Listeners.Add(listener);
        runtime.WriteLog(
            $"Listening for Reach clients on {address}:{ReachProtocol.ControlPort}.");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var transport = await listener.AcceptConnectionAsync(cancellationToken)
                    .ConfigureAwait(false);
                await AcceptAsync(transport, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException exception)
        {
            runtime.WriteLog($"Reach listener on {address} stopped: {exception.Message}");
        }
        finally
        {
            await listener.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal async Task RunQuicAsync(
        IPAddress address,
        X509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        QuicTransportListener listener;
        try
        {
            listener = await QuicTransportListener.ListenAsync(
                    new IPEndPoint(address, ReachProtocol.ControlPort),
                    new QuicTransportOptions
                    {
                        ServerCertificate = certificate,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            runtime.WriteLog(
                $"Could not listen for Reach QUIC clients on "
                + $"{address}:{ReachProtocol.ControlPort}: {exception.Message}");
            return;
        }

        runtime.Listeners.Add(listener);
        runtime.WriteLog(
            $"Listening for Reach QUIC clients on "
            + $"{address}:{ReachProtocol.ControlPort}.");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ITransportConnection transport;
                try
                {
                    transport = await listener.AcceptConnectionAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    await AcceptAsync(transport, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await transport.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            runtime.WriteLog(
                $"Reach QUIC listener on {address} stopped: {exception.Message}");
        }
        finally
        {
            await listener.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task AcceptAsync(
        ITransportConnection transport,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref runtime.ClientSequence);
        var connection = await ReachHostClientConnection.CreateAsync(
                id,
                transport,
                cancellationToken)
            .ConfigureAwait(false);
        runtime.Clients[id] = connection;
        _ = runtime.Remote.HandleAsync(connection, cancellationToken);
    }
}
