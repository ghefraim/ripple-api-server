using Microsoft.Extensions.Configuration;

namespace Application.Common.Configuration;

public class TokenConfiguration
{
    public required string Key { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string GoogleClientId { get; set; }
    public int AccessTokenLifetimeInMinutes { get; set; } = 30;
    public int RefreshTokenLifetimeInDays { get; set; } = 14;
    public int OtpLifetimeInMinutes { get; set; } = 5;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenLifetimeInMinutes);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenLifetimeInDays);
    public TimeSpan OtpLifetime => TimeSpan.FromMinutes(OtpLifetimeInMinutes);

    public static TokenConfiguration FromConfiguration(IConfiguration configuration)
    {
        return new TokenConfiguration
        {
            Key = configuration["JwtConfiguration:Key"] ?? throw new ArgumentNullException("JwtConfiguration:Key"),
            Issuer = configuration["JwtConfiguration:Issuer"] ?? throw new ArgumentNullException("JwtConfiguration:Issuer"),
            Audience = configuration["JwtConfiguration:Audience"] ?? throw new ArgumentNullException("JwtConfiguration:Audience"),
            GoogleClientId = configuration["Authentication:Google:ClientId"] ?? throw new ArgumentNullException("Authentication:Google:ClientId")
        };
    }
}