using Application.Domain.Enums;

namespace Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string UserEmail { get; }
    Role Role { get; }
    string DeviceInfo { get; }
    bool IsApiRequest { get; }
    Guid? OrganizationId { get; }
    OrganizationRole? OrganizationRole { get; }
}
