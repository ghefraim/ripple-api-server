using System.Security.Claims;
using Application.Domain.Constants;
using Microsoft.AspNetCore.Http;

namespace Application.UnitTests.Services;

public class CurrentUserServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<ILogger<CurrentUserService>> _loggerMock;
    private readonly CurrentUserService _sut;

    public CurrentUserServiceTests()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _loggerMock = new Mock<ILogger<CurrentUserService>>();
        _sut = new CurrentUserService(_httpContextAccessorMock.Object, _loggerMock.Object);
    }

    private void SetupHttpContext(ClaimsPrincipal? user = null, Dictionary<string, string>? headers = null)
    {
        var httpContext = new DefaultHttpContext();

        if (user != null)
        {
            httpContext.User = user;
        }

        if (headers != null)
        {
            foreach (var header in headers)
            {
                httpContext.Request.Headers[header.Key] = header.Value;
            }
        }

        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(
        Guid? userId = null,
        string? email = null,
        Guid? organizationId = null,
        OrganizationRole? organizationRole = null,
        Role? role = null)
    {
        var claims = new List<Claim>();

        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (organizationId.HasValue)
        {
            claims.Add(new Claim(CustomClaimTypes.OrganizationId, organizationId.Value.ToString()));
        }

        if (organizationRole.HasValue)
        {
            claims.Add(new Claim(CustomClaimTypes.OrganizationRole, organizationRole.Value.ToString()));
        }

        if (role.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    #region UserId Tests

    [Fact]
    public void UserId_WithAuthenticatedUser_ShouldReturnUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateAuthenticatedUser(userId: userId);
        SetupHttpContext(user);

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().Be(userId.ToString());
    }

    [Fact]
    public void UserId_WithUnauthenticatedUser_ShouldReturnNull()
    {
        // Arrange
        SetupHttpContext();

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void UserId_WithNullHttpContext_ShouldReturnNull()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void UserId_WithApiKeyHeader_ShouldReturnNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateAuthenticatedUser(userId: userId);
        SetupHttpContext(user, new Dictionary<string, string> { { "X-API-Key", "test-api-key" } });

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void UserId_WithoutNameIdentifierClaim_ShouldReturnNull()
    {
        // Arrange
        var user = CreateAuthenticatedUser(email: "test@example.com");
        SetupHttpContext(user);

        // Act
        var result = _sut.UserId;

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UserEmail Tests

    [Fact]
    public void UserEmail_WithAuthenticatedUser_ShouldReturnEmail()
    {
        // Arrange
        var email = "test@example.com";
        var user = CreateAuthenticatedUser(userId: Guid.NewGuid(), email: email);
        SetupHttpContext(user);

        // Act
        var result = _sut.UserEmail;

        // Assert
        result.Should().Be(email);
    }

    [Fact]
    public void UserEmail_WithUnauthenticatedUser_ShouldReturnSystemEmail()
    {
        // Arrange
        SetupHttpContext();

        // Act
        var result = _sut.UserEmail;

        // Assert
        result.Should().Be("system@unknown.com");
    }

    [Fact]
    public void UserEmail_WithNullHttpContext_ShouldReturnSystemEmail()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _sut.UserEmail;

        // Assert
        result.Should().Be("system@unknown.com");
    }

    [Fact]
    public void UserEmail_WithoutEmailClaim_ShouldReturnUnknownEmail()
    {
        // Arrange
        var user = CreateAuthenticatedUser(userId: Guid.NewGuid());
        SetupHttpContext(user);

        // Act
        var result = _sut.UserEmail;

        // Assert
        result.Should().Be("unknown@system.com");
    }

    #endregion

    #region Role Tests

    [Fact]
    public void Role_WithAdminRole_ShouldReturnAdmin()
    {
        // Arrange
        var user = CreateAuthenticatedUser(userId: Guid.NewGuid(), role: Role.Admin);
        SetupHttpContext(user);

        // Act
        var result = _sut.Role;

        // Assert
        result.Should().Be(Role.Admin);
    }

    [Fact]
    public void Role_WithUserRole_ShouldReturnUser()
    {
        // Arrange
        var user = CreateAuthenticatedUser(userId: Guid.NewGuid(), role: Role.User);
        SetupHttpContext(user);

        // Act
        var result = _sut.Role;

        // Assert
        result.Should().Be(Role.User);
    }

    [Fact]
    public void Role_WithUnauthenticatedUser_ShouldReturnUserDefault()
    {
        // Arrange
        SetupHttpContext();

        // Act
        var result = _sut.Role;

        // Assert
        result.Should().Be(Role.User);
    }

    [Fact]
    public void Role_WithoutRoleClaim_ShouldReturnUserDefault()
    {
        // Arrange
        var user = CreateAuthenticatedUser(userId: Guid.NewGuid());
        SetupHttpContext(user);

        // Act
        var result = _sut.Role;

        // Assert
        result.Should().Be(Role.User);
    }

    #endregion

    #region OrganizationId Tests

    [Fact]
    public void OrganizationId_WithValidClaim_ShouldReturnOrganizationId()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var user = CreateAuthenticatedUser(userId: Guid.NewGuid(), organizationId: organizationId);
        SetupHttpContext(user);

        // Act
        var result = _sut.OrganizationId;

        // Assert
        result.Should().Be(organizationId);
    }

    [Fact]
    public void OrganizationId_WithUnauthenticatedUser_ShouldReturnNull()
    {
        // Arrange
        SetupHttpContext();

        // Act
        var result = _sut.OrganizationId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void OrganizationId_WithNullHttpContext_ShouldReturnNull()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _sut.OrganizationId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void OrganizationId_WithEmptyGuidClaim_ShouldReturnNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(CustomClaimTypes.OrganizationId, Guid.Empty.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        SetupHttpContext(user);

        // Act
        var result = _sut.OrganizationId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void OrganizationId_WithWhitespaceClaim_ShouldReturnNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(CustomClaimTypes.OrganizationId, "   "),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        SetupHttpContext(user);

        // Act
        var result = _sut.OrganizationId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void OrganizationId_WithInvalidGuidClaim_ShouldReturnNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(CustomClaimTypes.OrganizationId, "not-a-guid"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        SetupHttpContext(user);

        // Act
        var result = _sut.OrganizationId;

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region OrganizationRole Tests

    [Fact]
    public void OrganizationRole_WithOwnerRole_ShouldReturnOwner()
    {
        // Arrange
        var user = CreateAuthenticatedUser(userId: Guid.NewGuid(), organizationRole: Domain.Enums.OrganizationRole.Owner);
        SetupHttpContext(user);

        // Act
        var result = _sut.OrganizationRole;

        // Assert
        result.Should().Be(Domain.Enums.OrganizationRole.Owner);
    }

    [Fact]
    public void OrganizationRole_WithMemberRole_ShouldReturnMember()
    {
        // Arrange
        var user = CreateAuthenticatedUser(userId: Guid.NewGuid(), organizationRole: Domain.Enums.OrganizationRole.Member);
        SetupHttpContext(user);

        // Act
        var result = _sut.OrganizationRole;

        // Assert
        result.Should().Be(Domain.Enums.OrganizationRole.Member);
    }

    [Fact]
    public void OrganizationRole_WithUnauthenticatedUser_ShouldReturnNull()
    {
        // Arrange
        SetupHttpContext();

        // Act
        var result = _sut.OrganizationRole;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void OrganizationRole_WithoutRoleClaim_ShouldReturnNull()
    {
        // Arrange
        var user = CreateAuthenticatedUser(userId: Guid.NewGuid());
        SetupHttpContext(user);

        // Act
        var result = _sut.OrganizationRole;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void OrganizationRole_WithInvalidRoleClaim_ShouldReturnNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(CustomClaimTypes.OrganizationRole, "InvalidRole"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        SetupHttpContext(user);

        // Act
        var result = _sut.OrganizationRole;

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeviceInfo Tests

    [Fact]
    public void DeviceInfo_WithDeviceInfoHeader_ShouldReturnDeviceInfo()
    {
        // Arrange
        var deviceInfo = "iPhone 12, iOS 15.0";
        SetupHttpContext(headers: new Dictionary<string, string> { { "DeviceInfo", deviceInfo } });

        // Act
        var result = _sut.DeviceInfo;

        // Assert
        result.Should().Be(deviceInfo);
    }

    [Fact]
    public void DeviceInfo_WithUserAgentHeader_ShouldReturnUserAgent()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = userAgent;
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _sut.DeviceInfo;

        // Assert
        result.Should().Be(userAgent);
    }

    [Fact]
    public void DeviceInfo_WithNullHttpContext_ShouldReturnUnknown()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _sut.DeviceInfo;

        // Assert
        result.Should().Be("Unknown");
    }

    #endregion

    #region IsApiRequest Tests

    [Fact]
    public void IsApiRequest_WithApiKeyHeader_ShouldReturnTrue()
    {
        // Arrange
        SetupHttpContext(headers: new Dictionary<string, string> { { "X-API-Key", "test-api-key" } });

        // Act
        var result = _sut.IsApiRequest;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsApiRequest_WithoutApiKeyHeader_ShouldReturnFalse()
    {
        // Arrange
        SetupHttpContext();

        // Act
        var result = _sut.IsApiRequest;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsApiRequest_WithEmptyApiKeyHeader_ShouldReturnFalse()
    {
        // Arrange
        SetupHttpContext(headers: new Dictionary<string, string> { { "X-API-Key", "" } });

        // Act
        var result = _sut.IsApiRequest;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsApiRequest_WithNullHttpContext_ShouldReturnFalse()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _sut.IsApiRequest;

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
