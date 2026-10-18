using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.SignalR.Client;
using Novolis.Reach.Protocol.SideChannel;
using Novolis.Reach.Transport;

namespace Novolis.Reach.Client;

/// <summary>
/// Maintains the optional SignalR status channel without putting media or
/// latency-sensitive input on the HTTP connection.
/// </summary>
internal sealed class ReachClientSideChannel(ReachClientSession session) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HubConnection? _connection;
    private CancellationTokenSource? _lifetimeCancellation;
    private CancellationTokenSource? _pollCancellation;
    private Task? _pollTask;
    private ReachSideChannelOffer? _offer;
    private int _closeRequested;

    internal ReachSideChannelState State { get; private set; } =
        ReachSideChannelState.Disconnected;

    internal async Task ConnectAsync(
        ReachSideChannelOffer offer,
        ReachTransportEndpoint primaryEndpoint,
        ReachPlatform platform,
        string clientName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(primaryEndpoint);

        await DisconnectAsync().ConfigureAwait(false);
        SetState(ReachSideChannelState.Connecting);
        Volatile.Write(ref _closeRequested, 0);

        var lifetime = new CancellationTokenSource();
        var connection = BuildConnection(
            offer,
            primaryEndpoint,
            lifetime.Token);
        _offer = offer;
        _lifetimeCancellation = lifetime;
        _connection = connection;

        try
        {
            await connection.StartAsync(cancellationToken).ConfigureAwait(false);
            await JoinAsync(
                    connection,
                    platform,
                    clientName,
                    cancellationToken)
                .ConfigureAwait(false);

            var pollCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                lifetime.Token);
            _pollCancellation = pollCancellation;
            _pollTask = PollStatusAsync(
                connection,
                pollCancellation.Token);
            SetState(ReachSideChannelState.Connected);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            lifetime.Cancel();
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            lifetime.Cancel();
            await DisconnectAsync().ConfigureAwait(false);
            SetState(ReachSideChannelState.Degraded);
            session.RaiseStatus(
                $"SignalR side channel unavailable; primary Reach transport remains active "
                + $"({exception.Message}).");
        }
    }

    internal async Task DisconnectAsync()
    {
        Volatile.Write(ref _closeRequested, 1);
        HubConnection? connection;
        CancellationTokenSource? lifetimeCancellation;
        CancellationTokenSource? pollCancellation;
        Task? pollTask;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            connection = Interlocked.Exchange(ref _connection, null);
            lifetimeCancellation = Interlocked.Exchange(
                ref _lifetimeCancellation,
                null);
            _offer = null;
            pollCancellation = Interlocked.Exchange(
                ref _pollCancellation,
                null);
            pollTask = Interlocked.Exchange(ref _pollTask, null);
        }
        finally
        {
            _gate.Release();
        }

        lifetimeCancellation?.Cancel();
        pollCancellation?.Cancel();
        if (pollTask is not null)
        {
            try
            {
                await pollTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (pollCancellation?.IsCancellationRequested == true)
            {
            }
        }

        pollCancellation?.Dispose();
        lifetimeCancellation?.Dispose();
        if (connection is not null)
        {
            try
            {
                await connection.StopAsync().ConfigureAwait(false);
            }
            catch
            {
            }

            await connection.DisposeAsync().ConfigureAwait(false);
        }

        SetState(ReachSideChannelState.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private HubConnection BuildConnection(
        ReachSideChannelOffer offer,
        ReachTransportEndpoint primaryEndpoint,
        CancellationToken lifetimeToken)
    {
        var address = new UriBuilder(
            offer.Scheme,
            primaryEndpoint.Address.Address.ToString(),
            offer.Port,
            offer.HubPath).Uri;
        var builder = new HubConnectionBuilder()
            .WithUrl(address, options =>
            {
                options.AccessTokenProvider = () =>
                    Task.FromResult<string?>(offer.AccessToken);
                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler http)
                    {
                        http.ServerCertificateCustomValidationCallback =
                            (_, certificate, _, errors) =>
                                ValidateCertificate(
                                    certificate,
                                    errors,
                                    offer.CertificatePin);
                    }

                    return handler;
                };
                options.WebSocketConfiguration = socket =>
                {
                    socket.RemoteCertificateValidationCallback =
                        (_, certificate, _, errors) =>
                            ValidateCertificate(
                                certificate,
                                errors,
                                offer.CertificatePin);
                };
            })
            .WithAutomaticReconnect(
            [
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(10),
            ]);
        var connection = builder.Build();
        connection.Reconnecting += _ =>
        {
            SetState(ReachSideChannelState.Reconnecting);
            return Task.CompletedTask;
        };
        connection.Reconnected += async _ =>
        {
            if (Volatile.Read(ref _closeRequested) != 0
                || lifetimeToken.IsCancellationRequested
                || _offer is null)
            {
                return;
            }

            try
            {
                await JoinAsync(
                        connection,
                        session._platform,
                        session._clientName ?? "Reach client",
                        lifetimeToken)
                    .ConfigureAwait(false);
                SetState(ReachSideChannelState.Connected);
            }
            catch (OperationCanceledException)
                when (lifetimeToken.IsCancellationRequested)
            {
            }
            catch
            {
                SetState(ReachSideChannelState.Degraded);
            }
        };
        connection.Closed += _ =>
        {
            if (Volatile.Read(ref _closeRequested) == 0)
            {
                SetState(
                    session.IsConnected
                        ? ReachSideChannelState.Degraded
                        : ReachSideChannelState.Disconnected);
            }

            return Task.CompletedTask;
        };
        return connection;
    }

    private async Task JoinAsync(
        HubConnection connection,
        ReachPlatform platform,
        string clientName,
        CancellationToken cancellationToken)
    {
        var welcome = await connection.InvokeAsync<ReachSideChannelWelcome>(
                "Join",
                new ReachSideChannelJoinRequest(
                    ReachProtocol.AppId,
                    ReachProtocol.Version,
                    clientName),
                cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(
                welcome.AppId,
                ReachProtocol.AppId,
                StringComparison.Ordinal)
            || !ReachProtocol.IsCompatible(welcome.ProtocolVersion))
        {
            throw new InvalidDataException(
                "The Reach side channel returned an incompatible protocol.");
        }

        var status = await connection.InvokeAsync<ReachHostStatus>(
                "GetStatus",
                cancellationToken)
            .ConfigureAwait(false);
        session.RaiseSideChannelStatus(status);
        _ = platform;
    }

    private async Task PollStatusAsync(
        HubConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(cancellationToken)
                       .ConfigureAwait(false))
            {
                try
                {
                    var status = await connection.InvokeAsync<ReachHostStatus>(
                            "GetStatus",
                            cancellationToken)
                        .ConfigureAwait(false);
                    session.RaiseSideChannelStatus(status);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    SetState(ReachSideChannelState.Degraded);
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void SetState(ReachSideChannelState state)
    {
        if (State == state)
            return;

        State = state;
        session.RaiseSideChannelStateChanged(state);
    }

    private static bool ValidateCertificate(
        X509Certificate? certificate,
        SslPolicyErrors errors,
        string? expectedPin)
    {
        if (certificate is null || string.IsNullOrWhiteSpace(expectedPin))
            return errors == SslPolicyErrors.None;

        try
        {
            using var certificate2 = new X509Certificate2(certificate);
            var actualPin = certificate2.GetCertHashString(
                System.Security.Cryptography.HashAlgorithmName.SHA256);
            return string.Equals(
                actualPin,
                expectedPin,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
