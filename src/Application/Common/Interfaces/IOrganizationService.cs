using Application.Common.Models.Organization;
using Application.Common.Models.User;
using Application.Domain.Entities;

namespace Application.Common.Interfaces;

public interface IOrganizationService
{
    Task<AuthenticationResponse> SelectOrganizationAsync(SelectOrganizationRequest request);
    Task<AuthenticationResponse> CreateOrganizationAsync(CreateOrganizationRequest request);
    Task EnsureUserHasDefaultOrganizationAsync(ApplicationUser user, CancellationToken cancellationToken = default);
    Task<ApplicationUser> GetUserWithOrganizationsAsync(Guid userId, CancellationToken cancellationToken = default);
}
