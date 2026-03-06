using Application.Common.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public class OperationsHub : Hub<IOperationsHubClient>
{
    private readonly ICurrentUserService _currentUserService;

    public OperationsHub(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override async Task OnConnectedAsync()
    {
        var organizationId = _currentUserService.OrganizationId;
        if (organizationId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, organizationId.Value.ToString());
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var organizationId = _currentUserService.OrganizationId;
        if (organizationId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, organizationId.Value.ToString());
        }

        await base.OnDisconnectedAsync(exception);
    }
}
