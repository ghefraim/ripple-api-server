using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Gates.DeleteGate;

[Authorize(Roles = "Owner")]
public record DeleteGateCommand(Guid Id) : IRequest;

public class DeleteGateCommandHandler(
    ApplicationDbContext context) : IRequestHandler<DeleteGateCommand>
{
    private readonly ApplicationDbContext _context = context;

    public async Task Handle(DeleteGateCommand request, CancellationToken cancellationToken)
    {
        var gate = await _context.Gates
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Gate), request.Id);

        _context.Gates.Remove(gate);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
