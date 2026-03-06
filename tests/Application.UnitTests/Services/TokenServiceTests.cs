using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Domain.Constants;
using Application.UnitTests.Common.Builders;
using Application.UnitTests.Common.Mocks;
using Microsoft.IdentityModel.Tokens;

namespace Application.UnitTests.Services;

public class TokenServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly MockDateTime _dateTime;
    private readonly TokenService _sut;

    public TokenServiceTests()
    {
        _configuration = MockConfiguration.CreateForTokenService();
        _dateTime = MockDateTime.Create();
        _sut = new TokenService(_configuration, _dateTime);
    }

    #region GenerateAccessToken Tests

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidJwtToken()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var roles = new List<string> { "User" };
        var organizationId = Guid.NewGuid();
        var organizationRole = OrganizationRole.Owner;

        // Act
        var token = _sut.GenerateAccessToken(user, roles, organizationId, organizationRole);

        // Assert
        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeUserIdClaim()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = ApplicationUserBuilder.Default().WithId(userId).Build();
        var roles = new List<string> { "User" };

        // Act
        var token = _sut.GenerateAccessToken(user, roles, null, null);

        // Assert
        var principal = _sut.ValidateToken(token);
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        userIdClaim.Should().NotBeNull();
        userIdClaim!.Value.Should().Be(userId.ToString());
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeEmailClaim()
    {
        // Arrange
        var email = "test@example.com";
        var user = ApplicationUserBuilder.Default().WithEmail(email).Build();
        var roles = new List<string> { "User" };

        // Act
        var token = _sut.GenerateAccessToken(user, roles, null, null);

        // Assert
        var principal = _sut.ValidateToken(token);
        var emailClaim = principal.FindFirst(ClaimTypes.Email);
        emailClaim.Should().NotBeNull();
        emailClaim!.Value.Should().Be(email);
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeOrganizationIdClaim()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var roles = new List<string> { "User" };
        var organizationId = Guid.NewGuid();

        // Act
        var token = _sut.GenerateAccessToken(user, roles, organizationId, OrganizationRole.Owner);

        // Assert
        var principal = _sut.ValidateToken(token);
        var orgClaim = principal.FindFirst(CustomClaimTypes.OrganizationId);
        orgClaim.Should().NotBeNull();
        orgClaim!.Value.Should().Be(organizationId.ToString());
    }

    [Fact]
    public void GenerateAccessToken_WithNullOrganizationId_ShouldIncludeEmptyGuidClaim()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var roles = new List<string> { "User" };

        // Act
        var token = _sut.GenerateAccessToken(user, roles, null, null);

        // Assert
        var principal = _sut.ValidateToken(token);
        var orgClaim = principal.FindFirst(CustomClaimTypes.OrganizationId);
        orgClaim.Should().NotBeNull();
        orgClaim!.Value.Should().Be(Guid.Empty.ToString());
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeOrganizationRoleClaim()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var roles = new List<string> { "User" };
        var organizationId = Guid.NewGuid();
        var organizationRole = OrganizationRole.Owner;

        // Act
        var token = _sut.GenerateAccessToken(user, roles, organizationId, organizationRole);

        // Assert
        var principal = _sut.ValidateToken(token);
        var roleClaim = principal.FindFirst(CustomClaimTypes.OrganizationRole);
        roleClaim.Should().NotBeNull();
        roleClaim!.Value.Should().Be("Owner");
    }

    [Fact]
    public void GenerateAccessToken_WithNullOrganizationRole_ShouldNotIncludeRoleClaim()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var roles = new List<string> { "User" };

        // Act
        var token = _sut.GenerateAccessToken(user, roles, Guid.NewGuid(), null);

        // Assert
        var principal = _sut.ValidateToken(token);
        var roleClaim = principal.FindFirst(CustomClaimTypes.OrganizationRole);
        roleClaim.Should().BeNull();
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeAllRoleClaims()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var roles = new List<string> { "User", "Admin" };

        // Act
        var token = _sut.GenerateAccessToken(user, roles, null, null);

        // Assert
        var principal = _sut.ValidateToken(token);
        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        roleClaims.Should().Contain("User");
        roleClaims.Should().Contain("Admin");
    }

    [Fact]
    public void GenerateAccessToken_ShouldSetCorrectExpiry()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var roles = new List<string> { "User" };
        var expectedExpiry = _dateTime.Now.AddMinutes(30); // Default lifetime

        // Act
        var token = _sut.GenerateAccessToken(user, roles, null, null);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region GenerateRefreshToken Tests

    [Fact]
    public void GenerateRefreshToken_ShouldReturnNonEmptyString()
    {
        // Act
        var token = _sut.GenerateRefreshToken();

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnBase64String()
    {
        // Act
        var token = _sut.GenerateRefreshToken();

        // Assert
        var act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnUniqueTokens()
    {
        // Act
        var tokens = Enumerable.Range(0, 100).Select(_ => _sut.GenerateRefreshToken()).ToList();

        // Assert
        tokens.Distinct().Count().Should().Be(100);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturn64ByteTokenWhenDecoded()
    {
        // Act
        var token = _sut.GenerateRefreshToken();

        // Assert
        var bytes = Convert.FromBase64String(token);
        bytes.Length.Should().Be(64);
    }

    #endregion

    #region GetRefreshTokenExpiry Tests

    [Fact]
    public void GetRefreshTokenExpiry_ShouldReturnFutureDate()
    {
        // Act
        var expiry = _sut.GetRefreshTokenExpiry();

        // Assert
        expiry.Should().BeAfter(_dateTime.Now);
    }

    [Fact]
    public void GetRefreshTokenExpiry_ShouldReturnDateBasedOnConfiguration()
    {
        // Default configuration is 14 days
        var expectedExpiry = _dateTime.Now.AddDays(14);

        // Act
        var expiry = _sut.GetRefreshTokenExpiry();

        // Assert
        expiry.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region ValidateToken Tests

    [Fact]
    public void ValidateToken_WithValidToken_ShouldReturnClaimsPrincipal()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var token = _sut.GenerateAccessToken(user, new List<string> { "User" }, null, null);

        // Act
        var principal = _sut.ValidateToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal.Identity.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_WithEmptyToken_ShouldThrowUnauthorizedAccessException()
    {
        // Act
        var act = () => _sut.ValidateToken(string.Empty);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("Token is empty or null.");
    }

    [Fact]
    public void ValidateToken_WithNullToken_ShouldThrowUnauthorizedAccessException()
    {
        // Act
        var act = () => _sut.ValidateToken(null!);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("Token is empty or null.");
    }

    [Fact]
    public void ValidateToken_WithInvalidSignature_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var token = _sut.GenerateAccessToken(user, new List<string> { "User" }, null, null);

        // Create a token with different key
        var differentConfig = MockConfiguration.CreateForTokenService(secretKey: "ADifferentSecretKeyThatIsAlso32BytesLong!");
        var differentService = new TokenService(differentConfig, _dateTime);
        var differentToken = differentService.GenerateAccessToken(user, new List<string> { "User" }, null, null);

        // Act - Try to validate token from different service
        var act = () => _sut.ValidateToken(differentToken);

        // Assert
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("Invalid token");
    }

    [Fact]
    public void ValidateToken_WithExpiredToken_ShouldThrowSecurityTokenExpiredException()
    {
        // Arrange - Create a service with a time in the past so the token is already expired
        var pastTime = DateTime.UtcNow.AddHours(-2);
        var pastDateTime = new MockDateTime(pastTime);
        var expiredTokenService = new TokenService(_configuration, pastDateTime);

        var user = ApplicationUserBuilder.Default().Build();
        var token = expiredTokenService.GenerateAccessToken(user, new List<string> { "User" }, null, null);

        // Act - Validate with current time (token was created 2 hours ago, expired 1.5 hours ago)
        var act = () => _sut.ValidateToken(token);

        // Assert
        act.Should().Throw<SecurityTokenExpiredException>();
    }

    [Fact]
    public void ValidateToken_WithMalformedToken_ShouldThrowUnauthorizedAccessException()
    {
        // Act
        var act = () => _sut.ValidateToken("not.a.valid.token");

        // Assert
        act.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("Invalid token");
    }

    #endregion

    #region ExtractOrganizationIdFromToken Tests

    [Fact]
    public void ExtractOrganizationIdFromToken_WithValidToken_ShouldReturnOrganizationId()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var organizationId = Guid.NewGuid();
        var token = _sut.GenerateAccessToken(user, new List<string> { "User" }, organizationId, OrganizationRole.Owner);

        // Act
        var result = _sut.ExtractOrganizationIdFromToken(token);

        // Assert
        result.Should().Be(organizationId);
    }

    [Fact]
    public void ExtractOrganizationIdFromToken_WithNullOrganizationId_ShouldReturnNull()
    {
        // Arrange
        var user = ApplicationUserBuilder.Default().Build();
        var token = _sut.GenerateAccessToken(user, new List<string> { "User" }, null, null);

        // Act
        var result = _sut.ExtractOrganizationIdFromToken(token);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractOrganizationIdFromToken_WithEmptyToken_ShouldReturnNull()
    {
        // Act
        var result = _sut.ExtractOrganizationIdFromToken(string.Empty);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractOrganizationIdFromToken_WithNullToken_ShouldReturnNull()
    {
        // Act
        var result = _sut.ExtractOrganizationIdFromToken(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractOrganizationIdFromToken_WithInvalidToken_ShouldReturnNull()
    {
        // Act
        var result = _sut.ExtractOrganizationIdFromToken("invalid.token.here");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractOrganizationIdFromToken_WithExpiredToken_ShouldReturnNull()
    {
        // Arrange - Create a service with a time in the past so the token is already expired
        var pastTime = DateTime.UtcNow.AddHours(-2);
        var pastDateTime = new MockDateTime(pastTime);
        var expiredTokenService = new TokenService(_configuration, pastDateTime);

        var user = ApplicationUserBuilder.Default().Build();
        var organizationId = Guid.NewGuid();
        var token = expiredTokenService.GenerateAccessToken(user, new List<string> { "User" }, organizationId, OrganizationRole.Owner);

        // Act - Extract with current time (token is already expired)
        var result = _sut.ExtractOrganizationIdFromToken(token);

        // Assert - Should return null because token validation fails
        result.Should().BeNull();
    }

    #endregion
}
