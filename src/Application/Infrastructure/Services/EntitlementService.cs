using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Infrastructure.Services;

public class EntitlementService : IEntitlementService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EntitlementService> _logger;
    private readonly BillingOptions _billingOptions;

    private const string UnlimitedValue = "unlimited";

    public EntitlementService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IMemoryCache cache,
        IOptions<BillingOptions> billingOptions,
        ILogger<EntitlementService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cache = cache;
        _billingOptions = billingOptions.Value;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> GetEntitlementsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(organizationId);

        if (_cache.TryGetValue(cacheKey, out Dictionary<string, string>? cached) && cached != null)
        {
            return cached;
        }

        var entitlements = await _context.Entitlements
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == organizationId && !e.IsDeleted)
            .Where(e => e.ExpiresAt == null || e.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, string>();

        foreach (var entitlement in entitlements)
        {
            if (!result.ContainsKey(entitlement.FeatureKey))
            {
                result[entitlement.FeatureKey] = entitlement.Value;
            }
            else if (entitlement.FeatureType == "limit")
            {
                var existingValue = result[entitlement.FeatureKey];
                if (existingValue == UnlimitedValue || entitlement.Value == UnlimitedValue)
                {
                    result[entitlement.FeatureKey] = UnlimitedValue;
                }
                else if (int.TryParse(existingValue, out var existing) && int.TryParse(entitlement.Value, out var newValue))
                {
                    result[entitlement.FeatureKey] = Math.Max(existing, newValue).ToString();
                }
            }
        }

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(_billingOptions.EntitlementCacheDurationMinutes));

        _cache.Set(cacheKey, result, cacheOptions);

        return result;
    }

    public async Task<bool> CanAccessAsync(string featureKey, CancellationToken cancellationToken = default)
    {
        var organizationId = _currentUserService.OrganizationId;
        if (organizationId == null)
        {
            return false;
        }

        var entitlements = await GetEntitlementsAsync(organizationId.Value, cancellationToken);

        if (!entitlements.TryGetValue(featureKey, out var value))
        {
            return false;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals(UnlimitedValue, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<int> GetLimitAsync(string featureKey, CancellationToken cancellationToken = default)
    {
        var organizationId = _currentUserService.OrganizationId;
        if (organizationId == null)
        {
            return 0;
        }

        var entitlements = await GetEntitlementsAsync(organizationId.Value, cancellationToken);

        if (!entitlements.TryGetValue(featureKey, out var value))
        {
            return 0;
        }

        if (value.Equals(UnlimitedValue, StringComparison.OrdinalIgnoreCase))
        {
            return int.MaxValue;
        }

        return int.TryParse(value, out var limit) ? limit : 0;
    }

    public async Task<FeatureCheckResult> CheckLimitAsync(string featureKey, int currentUsage, CancellationToken cancellationToken = default)
    {
        var limit = await GetLimitAsync(featureKey, cancellationToken);

        if (limit == int.MaxValue)
        {
            return new FeatureCheckResult(true, null, null, currentUsage);
        }

        if (currentUsage >= limit)
        {
            return new FeatureCheckResult(
                false,
                $"You have reached the limit of {limit} for {featureKey}. Please upgrade your plan.",
                limit,
                currentUsage);
        }

        return new FeatureCheckResult(true, null, limit, currentUsage);
    }

    public async Task ProvisionFromPlanAsync(Guid organizationId, Guid planId, CancellationToken cancellationToken = default)
    {
        var planFeatures = await _context.PlanFeatures
            .Where(pf => pf.PlanId == planId && !pf.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var feature in planFeatures)
        {
            await GrantAsync(
                organizationId,
                feature.FeatureKey,
                feature.FeatureType,
                feature.Value,
                EntitlementSource.Plan,
                planId,
                "Provisioned from plan",
                null,
                cancellationToken);
        }

        InvalidateCache(organizationId);

        _logger.LogInformation("Provisioned {Count} entitlements from plan {PlanId} for organization {OrganizationId}",
            planFeatures.Count, planId, organizationId);
    }

    public async Task GrantAsync(
        Guid organizationId,
        string featureKey,
        string featureType,
        string value,
        EntitlementSource source,
        Guid? sourceId = null,
        string? reason = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var existingEntitlement = await _context.Entitlements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                e => e.OrganizationId == organizationId &&
                     e.FeatureKey == featureKey &&
                     e.Source == source &&
                     !e.IsDeleted,
                cancellationToken);

        if (existingEntitlement != null)
        {
            existingEntitlement.Value = value;
            existingEntitlement.FeatureType = featureType;
            existingEntitlement.SourceId = sourceId;
            existingEntitlement.Reason = reason;
            existingEntitlement.ExpiresAt = expiresAt;

            if (source == EntitlementSource.Manual)
            {
                existingEntitlement.GrantedBy = _currentUserService.UserId;
            }
        }
        else
        {
            var entitlement = new Entitlement
            {
                OrganizationId = organizationId,
                FeatureKey = featureKey,
                FeatureType = featureType,
                Value = value,
                Source = source,
                SourceId = sourceId,
                Reason = reason,
                ExpiresAt = expiresAt,
                GrantedBy = source == EntitlementSource.Manual ? _currentUserService.UserId : null,
            };

            _context.Entitlements.Add(entitlement);
        }

        await _context.SaveChangesAsync(cancellationToken);
        InvalidateCache(organizationId);

        _logger.LogInformation("Granted entitlement {FeatureKey}={Value} to organization {OrganizationId} from source {Source}",
            featureKey, value, organizationId, source);
    }

    public async Task RevokeAsync(
        Guid organizationId,
        string featureKey,
        EntitlementSource source,
        CancellationToken cancellationToken = default)
    {
        var entitlement = await _context.Entitlements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                e => e.OrganizationId == organizationId &&
                     e.FeatureKey == featureKey &&
                     e.Source == source &&
                     !e.IsDeleted,
                cancellationToken);

        if (entitlement != null)
        {
            entitlement.IsDeleted = true;
            entitlement.DeletedOn = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            InvalidateCache(organizationId);

            _logger.LogInformation("Revoked entitlement {FeatureKey} from organization {OrganizationId}",
                featureKey, organizationId);
        }
    }

    public async Task RevokeAllFromSourceAsync(
        Guid organizationId,
        EntitlementSource source,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        var entitlements = await _context.Entitlements
            .IgnoreQueryFilters()
            .Where(e => e.OrganizationId == organizationId &&
                        e.Source == source &&
                        e.SourceId == sourceId &&
                        !e.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var entitlement in entitlements)
        {
            entitlement.IsDeleted = true;
            entitlement.DeletedOn = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        InvalidateCache(organizationId);

        _logger.LogInformation("Revoked {Count} entitlements from source {Source} for organization {OrganizationId}",
            entitlements.Count, source, organizationId);
    }

    private void InvalidateCache(Guid organizationId)
    {
        var cacheKey = GetCacheKey(organizationId);
        _cache.Remove(cacheKey);
    }

    private static string GetCacheKey(Guid organizationId)
    {
        return $"entitlements_{organizationId}";
    }
}
