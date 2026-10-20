using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Novolis.Reach.Transport;
using Novolis.Transports;
using Novolis.Transports.LocalIpc;
using Novolis.Windows.Sessions;

namespace Novolis.Reach.Host.Server;

internal sealed class ReachHostRuntime
{
    internal const string OperatorEndpoint = "Novolis.Reach.Host.Windows.Service";
    internal const string SessionEndpoint = "Novolis.Reach.Host.Windows";

    internal ReachHostRuntime(
        ILogger log,
        WindowsSessionManager sessions)
    {
        Log = log;
        Sessions = sessions;
        Device = ReachHostDeviceIdentity.Load(log);
        Broadcast = new ReachHostBroadcast(this);
        Session = new ReachHostSessionBridge(this);
        Files = new ReachHostFileTransfers(this);
        Operator = new ReachHostOperator(this);
        Remote = new ReachHostRemoteClient(this);
        Control = new ReachHostControlListeners(this);
        Media = new ReachHostMediaListeners(this);
        Quic = new ReachHostQuicStreams(this);
    }

    internal ILogger Log { get; }
    internal WindowsSessionManager Sessions { get; }
    internal ReachDeviceStatus Device { get; }
    internal ConcurrentDictionary<long, ReachHostClientConnection> Clients { get; } = new();
    internal ConcurrentDictionary<string, long> SideChannelTokens { get; } = new();
    internal ConcurrentDictionary<string, long> SideChannelConnections { get; } = new();
    internal ConcurrentDictionary<Guid, ReachHostFileTransfer> FileTransfers { get; } = new();
    internal ConcurrentBag<IAsyncDisposable> Listeners { get; } = [];
    internal ConcurrentBag<IAsyncDisposable> MediaListeners { get; } = [];
    internal object MessagesGate { get; } = new();
    internal object SessionStatusGate { get; } = new();
    internal List<string> Messages { get; } = [];
    internal SemaphoreSlim SessionGate { get; } = new(1, 1);
    internal CancellationTokenSource Lifetime { get; } = new();
    internal ReachPerformanceMetrics Performance { get; } = new();
    internal ILocalIpcConnection? SessionConnection;
    internal ITransportDatagramChannel? DatagramChannel;
    internal long ClientSequence;
    internal long LocalSequence;
    internal string[] Endpoints = [];
    internal string? SideChannelCertificatePin { get; set; }
    internal bool SharingPaused;
    internal int SideChannelStarted;
    internal int HostingStopped;
    internal DateTimeOffset NextSessionHelperLaunchAttempt = DateTimeOffset.MinValue;
    internal ReachUserSessionStatus? UserSessionStatus { get; private set; }

    internal ReachHostBroadcast Broadcast { get; }
    internal ReachHostSessionBridge Session { get; }
    internal ReachHostFileTransfers Files { get; }
    internal ReachHostOperator Operator { get; }
    internal ReachHostRemoteClient Remote { get; }
    internal ReachHostControlListeners Control { get; }
    internal ReachHostMediaListeners Media { get; }
    internal ReachHostQuicStreams Quic { get; }

    internal bool IsSideChannelAvailable =>
        Volatile.Read(ref SideChannelStarted) != 0;

    internal ReachSideChannelOffer? CreateSideChannelOffer(
        ReachHostClientConnection connection)
    {
        if (!IsSideChannelAvailable)
            return null;

        var token = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));
        SideChannelTokens[token] = connection.Id;
        connection.SideChannelToken = token;
        return new ReachSideChannelOffer(
            ReachProtocol.SideChannelScheme,
            ReachProtocol.SideChannelPort,
            ReachProtocol.SideChannelHubPath,
            token,
            SideChannelCertificatePin);
    }

    internal bool TryAuthorizeSideChannel(
        string accessToken,
        out long clientId) =>
        SideChannelTokens.TryGetValue(accessToken, out clientId)
        && Clients.ContainsKey(clientId);

    internal bool AttachSideChannel(
        string signalRConnectionId,
        string accessToken)
    {
        if (!TryAuthorizeSideChannel(accessToken, out var clientId))
            return false;

        SideChannelConnections[signalRConnectionId] = clientId;
        return true;
    }

    internal bool IsAuthorizedSideChannel(string signalRConnectionId) =>
        SideChannelConnections.ContainsKey(signalRConnectionId);

    internal void DetachSideChannel(string signalRConnectionId) =>
        SideChannelConnections.TryRemove(signalRConnectionId, out _);

    internal void RemoveSideChannelToken(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return;

        SideChannelTokens.TryRemove(accessToken, out var clientId);
        foreach (var pair in SideChannelConnections)
        {
            if (pair.Value == clientId)
                SideChannelConnections.TryRemove(pair.Key, out _);
        }
    }

    internal void WriteLog(string message)
    {
        Log.LogInformation("{Message}", message);
        lock (MessagesGate)
            Messages.Add($"{DateTimeOffset.Now:HH:mm:ss} {message}");
    }

    internal ReachHostStatus CreateStatus()
    {
        Sessions.TryGetActiveSession(out var session);
        var sessionConnection = Volatile.Read(ref SessionConnection);
        ReachUserSessionStatus userSession;
        lock (SessionStatusGate)
        {
            userSession = CreateCurrentUserSessionStatus(
                session,
                sessionConnection is not null);
        }
        lock (MessagesGate)
        {
            return new ReachHostStatus(
                Endpoints.Length == 0
                    ? "Waiting for LAN or Tailscale"
                    : Volatile.Read(ref HostingStopped) != 0
                        ? "Stopped by operator"
                    : sessionConnection is null
                        ? "Waiting for interactive session"
                        : "Running",
                Endpoints,
                Clients.Count,
                SharingPaused,
                session?.DomainName is { Length: > 0 } domain
                    ? $"{domain}\\{session.UserName}"
                    : session?.UserName,
                Messages.TakeLast(40).ToArray(),
                Performance.Snapshot(),
                Device,
                userSession);
        }
    }

    internal void SetInteractiveHelperState(
        ReachSessionLifecycleStatus lifecycle,
        bool helperConnected = true)
    {
        Sessions.TryGetActiveSession(out var session);
        lock (SessionStatusGate)
        {
            UserSessionStatus = CreateUserSessionStatus(
                session,
                lifecycle.State,
                helperConnected,
                lifecycle.CaptureActive ? "Capturing" : "Idle");
        }
    }

    internal void SetInteractiveHelperDisconnected()
    {
        Sessions.TryGetActiveSession(out var session);
        lock (SessionStatusGate)
        {
            UserSessionStatus = CreateUserSessionStatus(
                session,
                session is null ? "NoUser" : "WaitingForHelper",
                helperConnected: false,
                captureState: "Idle");
        }
    }

    private ReachUserSessionStatus CreateCurrentUserSessionStatus(
        WindowsSessionInfo? session,
        bool helperConnected)
    {
        if (session is null)
        {
            UserSessionStatus = new ReachUserSessionStatus(
                null,
                null,
                null,
                "NoUser",
                false,
                "Idle");
            return UserSessionStatus;
        }

        var previous = UserSessionStatus;
        if (previous is null
            || previous.SessionId != session.SessionId
            || !helperConnected)
        {
            UserSessionStatus = CreateUserSessionStatus(
                session,
                helperConnected ? "Ready" : "WaitingForHelper",
                helperConnected,
                helperConnected
                    && previous is not null
                    && previous.SessionId == session.SessionId
                    && string.Equals(
                        previous.CaptureState,
                        "Capturing",
                        StringComparison.Ordinal)
                    ? "Capturing"
                    : "Idle");
        }
        else
        {
            UserSessionStatus = previous with
            {
                DomainName = session.DomainName,
                UserName = session.UserName,
                HelperConnected = true,
            };
        }

        return UserSessionStatus;
    }

    private static ReachUserSessionStatus CreateUserSessionStatus(
        WindowsSessionInfo? session,
        string state,
        bool helperConnected,
        string captureState) =>
        new(
            session?.SessionId,
            session?.DomainName,
            session?.UserName,
            state,
            helperConnected,
            captureState);
}
