using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rules.GetRuleById;

[Authorize]
public record GetRuleByIdQuery(Guid Id) : IRequest<RuleDetailResponse>;

public record RuleDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    string RuleJson,
    DateTime CreatedOn);

public class GetRuleByIdQueryHandler(
    ApplicationDbContext context) : IRequestHandler<GetRuleByIdQuery, RuleDetailResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<RuleDetailResponse> Handle(GetRuleByIdQuery request, CancellationToken cancellationToken)
    {
        var rule = await _context.OperationalRules
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(OperationalRule), request.Id);

        return new RuleDetailResponse(
            rule.Id,
            rule.Name,
            rule.Description,
            rule.IsActive,
            rule.RuleJson,
            rule.CreatedOn);
    }
}
