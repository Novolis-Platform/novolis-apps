using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Novolis.Hours.Server;

/// <summary>Authenticated SignalR transport for live worktime view invalidation.</summary>
[Authorize]
public sealed class HoursHub : Hub
{
    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var actor = HoursPrincipalFactory.ToActor(
            Context.User ?? throw new HubException("An authenticated Hours principal is required."));
        await Groups.AddToGroupAsync(Context.ConnectionId, EmployeeGroup(actor.Id), Context.ConnectionAborted);
        if (actor.Role != Novolis.Hours.Domain.HoursActorRole.Employee)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdministratorsGroup, Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>Gets the SignalR group for one employee's projection.</summary>
    public static string EmployeeGroup(string employeeId) => $"employee:{employeeId}";

    /// <summary>Gets the shared review group for privileged roles.</summary>
    public const string AdministratorsGroup = "hours-reviewers";
}
