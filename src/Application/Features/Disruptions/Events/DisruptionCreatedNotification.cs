using MediatR;

namespace Application.Features.Disruptions.Events;

public record DisruptionCreatedNotification(
    Guid DisruptionId,
    Guid OrganizationId
) : INotification;
