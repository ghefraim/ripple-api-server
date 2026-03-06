using Application.Common.Interfaces;
using Application.Common.Security;

using MediatR;

namespace Application.Features.Billing.GetEntitlements;

[Authorize]
public record GetEntitlementsQuery : IRequest<EntitlementsResponse>;

public record EntitlementsResponse(
    Dictionary<string, string> Entitlements
);

public class GetEntitlementsQueryHandler(
    IEntitlementService entitlementService,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetEntitlementsQuery, EntitlementsResponse>
{
    private readonly IEntitlementService _entitlementService = entitlementService;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<EntitlementsResponse> Handle(GetEntitlementsQuery request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var entitlements = await _entitlementService.GetEntitlementsAsync(organizationId, cancellationToken);

        return new EntitlementsResponse(entitlements);
    }
}
