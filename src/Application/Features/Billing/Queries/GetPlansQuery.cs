using Application.Infrastructure.Persistence;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Billing.GetPlans;

public record GetPlansQuery : IRequest<List<PlanResponse>>;

public record PlanResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    string? StripeMonthlyPriceId,
    string? StripeAnnualPriceId,
    int SortOrder,
    List<PlanFeatureResponse> Features
);

public record PlanFeatureResponse(
    string FeatureKey,
    string FeatureType,
    string Value
);

public class GetPlansQueryHandler(ApplicationDbContext context)
    : IRequestHandler<GetPlansQuery, List<PlanResponse>>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<PlanResponse>> Handle(GetPlansQuery request, CancellationToken cancellationToken)
    {
        return await _context.Plans
            .Where(p => p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.SortOrder)
            .Select(p => new PlanResponse(
                p.Id,
                p.Name,
                p.Description,
                p.MonthlyPrice,
                p.AnnualPrice,
                p.StripeMonthlyPriceId,
                p.StripeAnnualPriceId,
                p.SortOrder,
                p.Features.Where(f => !f.IsDeleted).Select(f => new PlanFeatureResponse(
                    f.FeatureKey,
                    f.FeatureType,
                    f.Value
                )).ToList()
            ))
            .ToListAsync(cancellationToken);
    }
}
