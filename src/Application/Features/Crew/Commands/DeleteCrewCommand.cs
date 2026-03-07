using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Crew.DeleteCrew;

[Authorize(Roles = "Owner")]
public record DeleteCrewCommand(Guid Id) : IRequest;

public class DeleteCrewCommandHandler(
    ApplicationDbContext context) : IRequestHandler<DeleteCrewCommand>
{
    private readonly ApplicationDbContext _context = context;

    public async Task Handle(DeleteCrewCommand request, CancellationToken cancellationToken)
    {
        var crew = await _context.GroundCrews
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(GroundCrew), request.Id);

        _context.GroundCrews.Remove(crew);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
