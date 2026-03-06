using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rules.DeleteRule;

[Authorize]
public record DeleteRuleCommand(Guid Id) : IRequest;

public class DeleteRuleCommandHandler(
    ApplicationDbContext context) : IRequestHandler<DeleteRuleCommand>
{
    private readonly ApplicationDbContext _context = context;

    public async Task Handle(DeleteRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _context.OperationalRules
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(OperationalRule), request.Id);

        _context.OperationalRules.Remove(rule);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
