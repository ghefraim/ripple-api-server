using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Crew.UpdateCrew;

[Authorize(Roles = "Owner")]
public record UpdateCrewCommand(
    Guid Id,
    string Name,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    CrewStatus Status
) : IRequest<UpdateCrewResponse>;

public record UpdateCrewResponse(
    Guid Id,
    string Name,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    CrewStatus Status
);

public class UpdateCrewCommandValidator : AbstractValidator<UpdateCrewCommand>
{
    public UpdateCrewCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
    }
}

public class UpdateCrewCommandHandler(
    ApplicationDbContext context) : IRequestHandler<UpdateCrewCommand, UpdateCrewResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<UpdateCrewResponse> Handle(UpdateCrewCommand request, CancellationToken cancellationToken)
    {
        var crew = await _context.GroundCrews
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(GroundCrew), request.Id);

        crew.Name = request.Name;
        crew.ShiftStart = request.ShiftStart;
        crew.ShiftEnd = request.ShiftEnd;
        crew.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateCrewResponse(
            crew.Id,
            crew.Name,
            crew.ShiftStart,
            crew.ShiftEnd,
            crew.Status);
    }
}
