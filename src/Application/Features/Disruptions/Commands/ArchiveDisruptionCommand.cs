using Application.Common.Exceptions;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using MediatR;

namespace Application.Features.Disruptions.Commands;

public record ArchiveDisruptionCommand(Guid Id) : IRequest<Unit>;

public class ArchiveDisruptionCommandHandler(
    ApplicationDbContext context) : IRequestHandler<ArchiveDisruptionCommand, Unit>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Unit> Handle(ArchiveDisruptionCommand request, CancellationToken cancellationToken)
    {
        var disruption = await _context.Disruptions.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Disruption), request.Id);

        disruption.Status = DisruptionStatus.Archived;
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
