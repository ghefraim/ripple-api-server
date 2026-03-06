using Application.Domain.Enums;

namespace Application.Common.Models.Organization;

public class UserOrganizationResponse
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public OrganizationRole Role { get; set; }
}
