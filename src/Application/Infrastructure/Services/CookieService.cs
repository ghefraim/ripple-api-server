using Application.Common.Configuration;
using Application.Common.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Application.Infrastructure.Services;

public class CookieService : ICookieService
{
    private const string RefreshTokenCookieName = "refresh_token";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TokenConfiguration _configuration;

    public CookieService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = TokenConfiguration.FromConfiguration(configuration);
    }

    public void SetRefreshToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        _httpContextAccessor.HttpContext?.Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.None,
            Secure = true,
            MaxAge = _configuration.RefreshTokenLifetime,
        });
    }

    public string? GetRefreshToken()
    {
        var cookie = _httpContextAccessor.HttpContext?.Request.Cookies[RefreshTokenCookieName];
        return !string.IsNullOrEmpty(cookie) ? cookie : null;
    }

    public void DeleteRefreshToken()
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Append(RefreshTokenCookieName, string.Empty, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.None,
            Secure = true,
            MaxAge = TimeSpan.Zero,
        });
    }
}

