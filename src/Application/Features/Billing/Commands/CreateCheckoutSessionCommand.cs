using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Infrastructure.Persistence;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Billing.CreateCheckoutSession;

[Authorize(Roles = "Owner")]
public record CreateCheckoutSessionCommand(
    Guid PlanId,
    string BillingInterval,
    string SuccessUrl,
    string CancelUrl
) : IRequest<CreateCheckoutSessionResponse>;

public record CreateCheckoutSessionResponse(
    string SessionId,
    string SessionUrl
);

public class CreateCheckoutSessionCommandValidator : AbstractValidator<CreateCheckoutSessionCommand>
{
    public CreateCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.PlanId)
            .NotEmpty().WithMessage("Plan ID is required.");

        RuleFor(x => x.BillingInterval)
            .NotEmpty().WithMessage("Billing interval is required.")
            .Must(x => x == "monthly" || x == "annual")
            .WithMessage("Billing interval must be 'monthly' or 'annual'.");

        RuleFor(x => x.SuccessUrl)
            .NotEmpty().WithMessage("Success URL is required.");

        RuleFor(x => x.CancelUrl)
            .NotEmpty().WithMessage("Cancel URL is required.");
    }
}

public class CreateCheckoutSessionCommandHandler(
    ApplicationDbContext context,
    IStripeService stripeService,
    ISubscriptionService subscriptionService,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateCheckoutSessionCommand, CreateCheckoutSessionResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly IStripeService _stripeService = stripeService;
    private readonly ISubscriptionService _subscriptionService = subscriptionService;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<CreateCheckoutSessionResponse> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var plan = await _context.Plans
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive && !p.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Plan not found.");

        var priceId = request.BillingInterval == "annual"
            ? plan.StripeAnnualPriceId
            : plan.StripeMonthlyPriceId;

        if (string.IsNullOrEmpty(priceId))
        {
            throw new InvalidOperationException($"No Stripe price configured for {request.BillingInterval} billing.");
        }

        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Organization not found.");

        var owner = await _context.UserOrganizations
            .Include(uo => uo.User)
            .FirstOrDefaultAsync(uo => uo.OrganizationId == organizationId &&
                                        uo.Role == Domain.Enums.OrganizationRole.Owner &&
                                        !uo.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Organization owner not found.");

        var billingCustomer = await _subscriptionService.GetOrCreateBillingCustomerAsync(
            organizationId,
            owner.User!.Email!,
            organization.Name,
            cancellationToken);

        var idempotencyKey = $"checkout_{organizationId}_{request.PlanId}_{DateTime.UtcNow:yyyyMMddHHmm}";

        var session = await _stripeService.CreateCheckoutSessionAsync(
            billingCustomer.StripeCustomerId,
            priceId,
            request.SuccessUrl,
            request.CancelUrl,
            new Dictionary<string, string>
            {
                ["organizationId"] = organizationId.ToString(),
                ["planId"] = request.PlanId.ToString(),
            },
            idempotencyKey,
            cancellationToken);

        return new CreateCheckoutSessionResponse(session.Id, session.Url);
    }
}
