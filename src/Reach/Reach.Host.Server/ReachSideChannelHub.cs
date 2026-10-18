using Microsoft.AspNetCore.SignalR;
using Novolis.Reach.Protocol.SideChannel;

namespace Novolis.Reach.Host.Server;

/// <summary>
/// Read-only Reach status and diagnostics hub. Media and interactive input stay
/// on the primary Reach transport.
/// </summary>
internal sealed class ReachSideChannelHub(ReachHostRuntime runtime) : Hub
{
    public Task<ReachSideChannelWelcome> Join(
        ReachSideChannelJoinRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(
                request.AppId,
                ReachProtocol.AppId,
                StringComparison.Ordinal)
            || !ReachProtocol.IsCompatible(request.ProtocolVersion))
        {
            throw new HubException("Incompatible Reach side-channel client.");
        }

        var accessToken = ReadAccessToken();
        if (accessToken is null
            || !runtime.AttachSideChannel(Context.ConnectionId, accessToken))
        {
            throw new HubException("The Reach side-channel token is invalid.");
        }

        return Task.FromResult(
            new ReachSideChannelWelcome(
                ReachProtocol.AppId,
                ReachProtocol.Version,
                Environment.MachineName,
                ["status", "diagnostics"]));
    }

    public ReachHostStatus GetStatus()
    {
        if (!runtime.IsAuthorizedSideChannel(Context.ConnectionId))
            throw new HubException("Join the Reach side channel first.");

        return runtime.CreateStatus();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        runtime.DetachSideChannel(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private string? ReadAccessToken()
    {
        var http = Context.GetHttpContext();
        var token = http?.Request.Query["access_token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            var authorization = http?.Request.Headers.Authorization.ToString();
            if (authorization?.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                token = authorization["Bearer ".Length..].Trim();
            }
        }

        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
