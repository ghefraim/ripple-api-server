using System.Collections.Generic;

using Application.Common.Models.Organization;

namespace Application.Common.Models.User;

/// <summary>
/// User profile information including avatar
/// </summary>
public class UserProfileResponse : BaseUserInfo
{
    public string? AvatarUrl { get; set; }
    public List<UserOrganizationResponse> Organizations { get; set; } = new();
    public UserOrganizationResponse? SelectedOrganization { get; set; }
    public Guid? SelectedOrganizationId { get; set; }
}
