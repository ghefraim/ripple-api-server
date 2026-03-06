using System.Security.Claims;

using Application.Domain.Entities;
using Application.Domain.Enums;

namespace Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles, Guid? organizationId, OrganizationRole? organizationRole);
    string GenerateRefreshToken();
    ClaimsPrincipal ValidateToken(string token);
    DateTime GetRefreshTokenExpiry();
    Guid? ExtractOrganizationIdFromToken(string token);
}
