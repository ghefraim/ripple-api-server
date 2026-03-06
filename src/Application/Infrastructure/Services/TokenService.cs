using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Application.Common.Configuration;
using Application.Common.Interfaces;
using Application.Domain.Constants;
using Application.Domain.Entities;
using Application.Domain.Enums;

using Ardalis.GuardClauses;

using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Application.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly TokenConfiguration _configuration;
    private readonly IDateTime _dateTime;

    public TokenService(IConfiguration configuration, IDateTime dateTime)
    {
        _configuration = TokenConfiguration.FromConfiguration(configuration);
        _dateTime = dateTime;
    }

    public string GenerateAccessToken(ApplicationUser user, IList<string> roles, Guid? organizationId, OrganizationRole? organizationRole)
    {
        Guard.Against.Null(_configuration);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
        };

        var orgClaimValue = organizationId?.ToString() ?? Guid.Empty.ToString();
        claims.Add(new Claim(CustomClaimTypes.OrganizationId, orgClaimValue));

        if (organizationRole.HasValue)
        {
            claims.Add(new Claim(CustomClaimTypes.OrganizationRole, organizationRole.Value.ToString()));
        }

        // Add roles to claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            claims: claims,
            expires: _dateTime.Now.Add(_configuration.AccessTokenLifetime),
            audience: _configuration.Audience,
            issuer: _configuration.Issuer,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public DateTime GetRefreshTokenExpiry()
    {
        return _dateTime.Now.Add(_configuration.RefreshTokenLifetime);
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new UnauthorizedAccessException("Token is empty or null.");
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration.Key);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidIssuer = _configuration.Issuer,
                ValidAudience = _configuration.Audience,

                // set clockskew to zero so tokens expire exactly at token expiration time (instead of 5 minutes later)
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;

            return new ClaimsPrincipal(new ClaimsIdentity(jwtToken.Claims, "jwt"));
        }
        catch (SecurityTokenExpiredException)
        {
            // Re-throw expired token exceptions so ASP.NET auth middleware can return 401
            throw;
        }
        catch (Exception)
        {
            // For other token validation errors, throw unauthorized to get 401 response
            throw new UnauthorizedAccessException("Invalid token");
        }
    }

    public Guid? ExtractOrganizationIdFromToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            // CRITICAL: Must validate signature before trusting claims
            var principal = ValidateToken(token);

            var orgClaim = principal.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.OrganizationId);
            if (orgClaim == null || string.IsNullOrWhiteSpace(orgClaim.Value))
            {
                return null;
            }

            if (Guid.TryParse(orgClaim.Value, out var organizationId) && organizationId != Guid.Empty)
            {
                return organizationId;
            }

            return null;
        }
        catch
        {
            // Token validation failed - return null to indicate no valid org
            return null;
        }
    }
}
