using Application.Common.Security;
using Application.Infrastructure.Persistence;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rules.GetRules;

[Authorize]
public record GetRulesQuery : IRequest<List<RuleResponse>>;

public record RuleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    string RuleJson);

public class GetRulesQueryHandler(
    ApplicationDbContext context) : IRequestHandler<GetRulesQuery, List<RuleResponse>>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<RuleResponse>> Handle(GetRulesQuery request, CancellationToken cancellationToken)
    {
        return await _context.OperationalRules
            .OrderBy(r => r.Name)
            .Select(r => new RuleResponse(
                r.Id,
                r.Name,
                r.Description,
                r.IsActive,
                r.RuleJson))
            .ToListAsync(cancellationToken);
    }
}
