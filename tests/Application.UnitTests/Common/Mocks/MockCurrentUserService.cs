namespace Application.UnitTests.Common.Mocks;

public class MockCurrentUserService : ICurrentUserService
{
    public string? UserId { get; set; } = Guid.NewGuid().ToString();
    public string UserEmail { get; set; } = "test@example.com";
    public Role Role { get; set; } = Role.User;
    public string DeviceInfo { get; set; } = "Test Device";
    public bool IsApiRequest { get; set; }
    public Guid? OrganizationId { get; set; } = Guid.NewGuid();
    public OrganizationRole? OrganizationRole { get; set; } = Domain.Enums.OrganizationRole.Owner;

    public static MockCurrentUserService Create(
        Guid? userId = null,
        Guid? organizationId = null,
        OrganizationRole? organizationRole = null)
    {
        return new MockCurrentUserService
        {
            UserId = (userId ?? Guid.NewGuid()).ToString(),
            OrganizationId = organizationId ?? Guid.NewGuid(),
            OrganizationRole = organizationRole ?? Domain.Enums.OrganizationRole.Owner,
        };
    }

    public static MockCurrentUserService CreateOwner(Guid organizationId)
    {
        return new MockCurrentUserService
        {
            OrganizationId = organizationId,
            OrganizationRole = Domain.Enums.OrganizationRole.Owner,
        };
    }

    public static MockCurrentUserService CreateMember(Guid organizationId)
    {
        return new MockCurrentUserService
        {
            OrganizationId = organizationId,
            OrganizationRole = Domain.Enums.OrganizationRole.Member,
        };
    }

    public static MockCurrentUserService CreateUnauthenticated()
    {
        return new MockCurrentUserService
        {
            UserId = null,
            OrganizationId = null,
            OrganizationRole = null,
        };
    }

    public static Mock<ICurrentUserService> CreateMock(
        Guid? userId = null,
        Guid? organizationId = null,
        OrganizationRole? organizationRole = null)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(x => x.UserId).Returns((userId ?? Guid.NewGuid()).ToString());
        mock.Setup(x => x.UserEmail).Returns("test@example.com");
        mock.Setup(x => x.Role).Returns(Role.User);
        mock.Setup(x => x.DeviceInfo).Returns("Test Device");
        mock.Setup(x => x.IsApiRequest).Returns(false);
        mock.Setup(x => x.OrganizationId).Returns(organizationId ?? Guid.NewGuid());
        mock.Setup(x => x.OrganizationRole).Returns(organizationRole ?? Domain.Enums.OrganizationRole.Owner);
        return mock;
    }
}
