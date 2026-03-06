using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Enums;

using FluentValidation;

using MediatR;

namespace Application.Features.Billing.GrantEntitlement;

[Authorize(Roles = "Admin")]
public record GrantEntitlementCommand(
    Guid OrganizationId,
    string FeatureKey,
    string FeatureType,
    string Value,
    string Source,
    string? Reason,
    DateTime? ExpiresAt
) : IRequest<GrantEntitlementResponse>;

public record GrantEntitlementResponse(
    bool Success,
    string Message
);

public class GrantEntitlementCommandValidator : AbstractValidator<GrantEntitlementCommand>
{
    public GrantEntitlementCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID is required.");

        RuleFor(x => x.FeatureKey)
            .NotEmpty().WithMessage("Feature key is required.")
            .MaximumLength(100);

        RuleFor(x => x.FeatureType)
            .NotEmpty().WithMessage("Feature type is required.")
            .Must(x => x == "limit" || x == "boolean" || x == "tier")
            .WithMessage("Feature type must be 'limit', 'boolean', or 'tier'.");

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Value is required.")
            .MaximumLength(200);

        RuleFor(x => x.Source)
            .NotEmpty().WithMessage("Source is required.")
            .Must(x => x == "Manual" || x == "Promo" || x == "Legacy")
            .WithMessage("Source must be 'Manual', 'Promo', or 'Legacy'.");
    }
}

public class GrantEntitlementCommandHandler(IEntitlementService entitlementService)
    : IRequestHandler<GrantEntitlementCommand, GrantEntitlementResponse>
{
    private readonly IEntitlementService _entitlementService = entitlementService;

    public async Task<GrantEntitlementResponse> Handle(GrantEntitlementCommand request, CancellationToken cancellationToken)
    {
        var source = Enum.Parse<EntitlementSource>(request.Source);

        await _entitlementService.GrantAsync(
            request.OrganizationId,
            request.FeatureKey,
            request.FeatureType,
            request.Value,
            source,
            null,
            request.Reason,
            request.ExpiresAt,
            cancellationToken);

        return new GrantEntitlementResponse(
            true,
            $"Entitlement '{request.FeatureKey}' granted to organization."
        );
    }
}
