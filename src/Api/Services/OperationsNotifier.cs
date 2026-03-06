using Api.Hubs;

using Application.Common.Interfaces;

using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public class OperationsNotifier : IOperationsNotifier
{
    private readonly IHubContext<OperationsHub, IOperationsHubClient> _hubContext;

    public OperationsNotifier(IHubContext<OperationsHub, IOperationsHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyDisruptionReported(Guid organizationId, DisruptionReportedEvent data)
        => _hubContext.Clients.Group(organizationId.ToString()).DisruptionReported(data);

    public Task NotifyCascadeComputed(Guid organizationId, CascadeComputedEvent data)
        => _hubContext.Clients.Group(organizationId.ToString()).CascadeComputed(data);

    public Task NotifyActionPlanGenerated(Guid organizationId, ActionPlanGeneratedEvent data)
        => _hubContext.Clients.Group(organizationId.ToString()).ActionPlanGenerated(data);
}
