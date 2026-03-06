using Application.Domain.Enums;

namespace Application.Common.Interfaces;

public record FeatureCheckResult(bool IsAllowed, string? Message = null, int? Limit = null, int? CurrentUsage = null);

public interface IEntitlementService
{
    Task<Dictionary<string, string>> GetEntitlementsAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<bool> CanAccessAsync(string featureKey, CancellationToken cancellationToken = default);

    Task<int> GetLimitAsync(string featureKey, CancellationToken cancellationToken = default);

    Task<FeatureCheckResult> CheckLimitAsync(string featureKey, int currentUsage, CancellationToken cancellationToken = default);

    Task ProvisionFromPlanAsync(Guid organizationId, Guid planId, CancellationToken cancellationToken = default);

    Task GrantAsync(
        Guid organizationId,
        string featureKey,
        string featureType,
        string value,
        EntitlementSource source,
        Guid? sourceId = null,
        string? reason = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        Guid organizationId,
        string featureKey,
        EntitlementSource source,
        CancellationToken cancellationToken = default);

    Task RevokeAllFromSourceAsync(
        Guid organizationId,
        EntitlementSource source,
        Guid sourceId,
        CancellationToken cancellationToken = default);
}
