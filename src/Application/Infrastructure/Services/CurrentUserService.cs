using System.Security.Claims;

using Application.Common.Interfaces;
using Application.Domain.Constants;
using Application.Domain.Enums;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Application.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrentUserService> _logger;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<CurrentUserService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public string? UserId => GetCurrentUserId();
    public string UserEmail => GetCurrentUserEmail();
    public Role Role => GetCurrentUserRole();
    public string DeviceInfo => GetDeviceInfo();
    public bool IsApiRequest => GetIsApiRequest();
    public Guid? OrganizationId => GetCurrentOrganizationId();
    public OrganizationRole? OrganizationRole => GetCurrentOrganizationRole();

    public string? GetCurrentUserId()
    {
        // First check if it's an API request
        if (IsApiRequest)
        {
            // We'll use a different mechanism to validate API keys
            // This will be handled by ApiKeyAuthorizationBehavior 
            // to avoid circular dependencies
            return null;
        }

        // Fall back to JWT authentication from HttpContext user claims
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var userIdClaim = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return null;
            }

            return userIdClaim.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract user ID from claims");
            return null;
        }
    }

    public string GetCurrentUserEmail()
    {
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return "system@unknown.com";
            }

            var emailClaim = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email);
            return emailClaim?.Value ?? "unknown@system.com";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract user email from claims");
            return "system@error.com";
        }
    }

    public Role GetCurrentUserRole()
    {
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return Role.User;
            }

            var roleClaim = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role);
            if (roleClaim?.Value != null && Enum.TryParse<Role>(roleClaim.Value, out var role))
            {
                return role;
            }

            return Role.User;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract user role from claims");
            return Role.User;
        }
    }

    private Guid? GetCurrentOrganizationId()
    {
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var organizationClaim = user.Claims.FirstOrDefault(x => x.Type == CustomClaimTypes.OrganizationId);
            if (organizationClaim == null || string.IsNullOrWhiteSpace(organizationClaim.Value))
            {
                return null;
            }

            if (Guid.TryParse(organizationClaim.Value, out var organizationId) && organizationId != Guid.Empty)
            {
                return organizationId;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract organization ID from claims");
            return null;
        }
    }

    private OrganizationRole? GetCurrentOrganizationRole()
    {
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var orgRoleClaim = user.Claims.FirstOrDefault(x => x.Type == CustomClaimTypes.OrganizationRole);
            if (orgRoleClaim?.Value != null && Enum.TryParse<OrganizationRole>(orgRoleClaim.Value, out var role))
            {
                return role;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract organization role from claims");
            return null;
        }
    }

    private string GetDeviceInfo()
    {
        // For mobile apps get the device info from the CUSTOM request headers
        var deviceInfo = _httpContextAccessor.HttpContext?.Request.Headers["DeviceInfo"].ToString() ?? "Unknown";
        if (!string.IsNullOrEmpty(deviceInfo))
        {
            return deviceInfo;
        }

        // For web browsers get the device info from the user agent header
        var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? "Unknown";
        return userAgent;
    }

    private bool GetIsApiRequest()
    {
        var apiKey = _httpContextAccessor.HttpContext?.Request.Headers["X-API-Key"].ToString();
        return !string.IsNullOrEmpty(apiKey);
    }
}
