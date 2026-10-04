using System.Collections.Concurrent;
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
    internal ConcurrentDictionary<long, ReachHostClientConnection> Clients { get; } = new();
    internal ConcurrentDictionary<Guid, ReachHostFileTransfer> FileTransfers { get; } = new();
    internal ConcurrentBag<IAsyncDisposable> Listeners { get; } = [];
    internal ConcurrentBag<IAsyncDisposable> MediaListeners { get; } = [];
    internal object MessagesGate { get; } = new();
    internal List<string> Messages { get; } = [];
    internal SemaphoreSlim SessionGate { get; } = new(1, 1);
    internal CancellationTokenSource Lifetime { get; } = new();
    internal ReachPerformanceMetrics Performance { get; } = new();
    internal ILocalIpcConnection? SessionConnection;
    internal ITransportDatagramChannel? DatagramChannel;
    internal long ClientSequence;
    internal long LocalSequence;
    internal string[] Endpoints = [];
    internal bool SharingPaused;
    internal int HostingStopped;
    internal DateTimeOffset NextSessionHelperLaunchAttempt = DateTimeOffset.MinValue;

    internal ReachHostBroadcast Broadcast { get; }
    internal ReachHostSessionBridge Session { get; }
    internal ReachHostFileTransfers Files { get; }
    internal ReachHostOperator Operator { get; }
    internal ReachHostRemoteClient Remote { get; }
    internal ReachHostControlListeners Control { get; }
    internal ReachHostMediaListeners Media { get; }
    internal ReachHostQuicStreams Quic { get; }

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
                Performance.Snapshot());
        }
    }
}
