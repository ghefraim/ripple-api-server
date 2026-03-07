using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Crew.AddCrewContact;

[Authorize(Roles = "Owner")]
public record AddCrewContactCommand(
    Guid CrewId,
    string Name,
    string PhoneNumber) : IRequest<AddCrewContactResponse>;

public record AddCrewContactResponse(Guid Id, string Name, string PhoneNumber, bool TelegramLinked);

public class AddCrewContactCommandValidator : AbstractValidator<AddCrewContactCommand>
{
    public AddCrewContactCommandValidator()
    {
        RuleFor(x => x.CrewId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(30);
    }
}

public class AddCrewContactCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<AddCrewContactCommand, AddCrewContactResponse>
{
    public async Task<AddCrewContactResponse> Handle(AddCrewContactCommand request, CancellationToken cancellationToken)
    {
        var organizationId = currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var crew = await context.GroundCrews
            .FirstOrDefaultAsync(c => c.Id == request.CrewId, cancellationToken)
            ?? throw new NotFoundException(nameof(GroundCrew), request.CrewId);

        var contact = new CrewContact
        {
            OrganizationId = organizationId,
            CrewId = crew.Id,
            Name = request.Name.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
        };

        context.CrewContacts.Add(contact);
        await context.SaveChangesAsync(cancellationToken);

        return new AddCrewContactResponse(contact.Id, contact.Name, contact.PhoneNumber, false);
    }
}
