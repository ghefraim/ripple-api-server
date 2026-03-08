namespace Application.Common.Interfaces;

public interface IOperationsNotifier
{
    Task NotifyDisruptionReported(Guid organizationId, DisruptionReportedEvent data);
    Task NotifyCascadeComputed(Guid organizationId, CascadeComputedEvent data);
    Task NotifyActionPlanGenerated(Guid organizationId, ActionPlanGeneratedEvent data);
    Task NotifyActionPlanUpdated(Guid organizationId, ActionPlanUpdatedEvent data);
}
