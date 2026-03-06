using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Enums;

using FluentValidation;

using MediatR;

namespace Application.Features.Billing.RevokeEntitlement;

[Authorize(Roles = "Admin")]
public record RevokeEntitlementCommand(
    Guid OrganizationId,
    string FeatureKey,
    string Source
) : IRequest<RevokeEntitlementResponse>;

public record RevokeEntitlementResponse(
    bool Success,
    string Message
);

public class RevokeEntitlementCommandValidator : AbstractValidator<RevokeEntitlementCommand>
{
    public RevokeEntitlementCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.FeatureKey)
            .NotEmpty().WithMessage("Feature key is required.");

        RuleFor(x => x.Source)
            .NotEmpty().WithMessage("Source is required.")
            .Must(x => x == "Manual" || x == "Promo" || x == "Legacy")
            .WithMessage("Source must be 'Manual', 'Promo', or 'Legacy'.");
    }
}

public class RevokeEntitlementCommandHandler(IEntitlementService entitlementService)
    : IRequestHandler<RevokeEntitlementCommand, RevokeEntitlementResponse>
{
    private readonly IEntitlementService _entitlementService = entitlementService;

    public async Task<RevokeEntitlementResponse> Handle(RevokeEntitlementCommand request, CancellationToken cancellationToken)
    {
        var source = Enum.Parse<EntitlementSource>(request.Source);

        await _entitlementService.RevokeAsync(
            request.OrganizationId,
            request.FeatureKey,
            source,
            cancellationToken);

        return new RevokeEntitlementResponse(
            true,
            $"Entitlement '{request.FeatureKey}' revoked from organization."
        );
    }
}
