using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Billing.CreateCustomerPortalSession;

[Authorize(Roles = "Owner")]
public record CreateCustomerPortalSessionCommand(
    string ReturnUrl
) : IRequest<CreateCustomerPortalSessionResponse>;

public record CreateCustomerPortalSessionResponse(
    string SessionUrl
);

public class CreateCustomerPortalSessionCommandValidator : AbstractValidator<CreateCustomerPortalSessionCommand>
{
    public CreateCustomerPortalSessionCommandValidator()
    {
        RuleFor(x => x.ReturnUrl)
            .NotEmpty().WithMessage("Return URL is required.");
    }
}

public class CreateCustomerPortalSessionCommandHandler(
    ApplicationDbContext context,
    IStripeService stripeService,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateCustomerPortalSessionCommand, CreateCustomerPortalSessionResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly IStripeService _stripeService = stripeService;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<CreateCustomerPortalSessionResponse> Handle(CreateCustomerPortalSessionCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var billingCustomer = await _context.BillingCustomers
            .FirstOrDefaultAsync(bc => bc.EntityType == BillableEntityType.Organization &&
                                        bc.EntityId == organizationId &&
                                        !bc.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("No billing customer found. Please subscribe to a plan first.");

        var session = await _stripeService.CreateBillingPortalSessionAsync(
            billingCustomer.StripeCustomerId,
            request.ReturnUrl,
            cancellationToken);

        return new CreateCustomerPortalSessionResponse(session.Url);
    }
}
