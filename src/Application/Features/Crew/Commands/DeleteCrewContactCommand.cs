using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Crew.DeleteCrewContact;

[Authorize(Roles = "Owner")]
public record DeleteCrewContactCommand(Guid Id) : IRequest;

public class DeleteCrewContactCommandHandler(
    ApplicationDbContext context) : IRequestHandler<DeleteCrewContactCommand>
{
    public async Task Handle(DeleteCrewContactCommand request, CancellationToken cancellationToken)
    {
        var contact = await context.CrewContacts
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CrewContact), request.Id);

        context.CrewContacts.Remove(contact);
        await context.SaveChangesAsync(cancellationToken);
    }
}
