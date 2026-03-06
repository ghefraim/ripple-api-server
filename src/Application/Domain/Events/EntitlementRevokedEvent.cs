using Application.Domain.Common;

namespace Application.Domain.Events;

public sealed class EntitlementRevokedEvent(Guid organizationId, string featureKey) : BaseEvent
{
    public Guid OrganizationId { get; } = organizationId;
    public string FeatureKey { get; } = featureKey;
}
