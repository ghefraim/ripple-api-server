using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Crew.CreateCrew;

[Authorize(Roles = "Owner")]
public record CreateCrewCommand(
    string Name,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd
) : IRequest<CreateCrewResponse>;

public record CreateCrewResponse(
    Guid Id,
    string Name,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    CrewStatus Status
);

public class CreateCrewCommandValidator : AbstractValidator<CreateCrewCommand>
{
    public CreateCrewCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
    }
}

public class CreateCrewCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<CreateCrewCommand, CreateCrewResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<CreateCrewResponse> Handle(CreateCrewCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new NotFoundException("AirportConfig", organizationId);

        var crew = new GroundCrew
        {
            OrganizationId = organizationId,
            AirportId = airport.Id,
            Name = request.Name,
            ShiftStart = request.ShiftStart,
            ShiftEnd = request.ShiftEnd,
            Status = CrewStatus.Available,
        };

        _context.GroundCrews.Add(crew);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateCrewResponse(
            crew.Id,
            crew.Name,
            crew.ShiftStart,
            crew.ShiftEnd,
            crew.Status);
    }
}
