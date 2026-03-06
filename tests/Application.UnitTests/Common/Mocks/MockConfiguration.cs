namespace Application.UnitTests.Common.Mocks;

public static class MockConfiguration
{
    public static IConfiguration CreateForTokenService(
        string? secretKey = null,
        string? issuer = null,
        string? audience = null,
        string? googleClientId = null)
    {
        // Generate a sufficiently long key for HS256 (minimum 256 bits = 32 bytes)
        var key = secretKey ?? "ThisIsATestSecretKeyThatIsAtLeast32BytesLong!";

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "JwtConfiguration:Key", key },
            { "JwtConfiguration:Issuer", issuer ?? "TestIssuer" },
            { "JwtConfiguration:Audience", audience ?? "TestAudience" },
            { "Authentication:Google:ClientId", googleClientId ?? "test-google-client-id" },
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    public static IConfiguration CreateForBilling(
        int entitlementCacheDurationMinutes = 5)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Billing:EntitlementCacheDurationMinutes", entitlementCacheDurationMinutes.ToString() },
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    public static IConfiguration CreateEmpty()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
    }
}
