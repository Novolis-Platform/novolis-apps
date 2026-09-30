using System.Net;
using System.Security.Cryptography;
using Novolis.Reach.Protocol;
using Novolis.Reach.Transport;
using Novolis.Transports;
using Novolis.Transports.Framing;
using Novolis.Transports.Udp;

namespace Novolis.Reach.Client;

/// <summary>Describes the observable state of a Reach client session.</summary>
public enum ReachSessionState
{
    Disconnected,
    Connecting,
    Connected,
    Streaming,
    Lost,
}
