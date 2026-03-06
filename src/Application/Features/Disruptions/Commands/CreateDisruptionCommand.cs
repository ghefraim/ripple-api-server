using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Features.Disruptions.Events;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disruptions.CreateDisruption;

[Authorize]
public record CreateDisruptionCommand(
    Guid FlightId,
    DisruptionType Type,
    string? DetailsJson
) : IRequest<CreateDisruptionResponse>;

public record CreateDisruptionResponse(
    Guid Id,
    Guid FlightId,
    string FlightNumber,
    DisruptionType Type,
    string DetailsJson,
    string? ReportedBy,
    DateTime ReportedAt
);

public class CreateDisruptionCommandValidator : AbstractValidator<CreateDisruptionCommand>
{
    public CreateDisruptionCommandValidator()
    {
        RuleFor(x => x.FlightId)
            .NotEmpty().WithMessage("FlightId is required.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid disruption type.");
    }
}

public class CreateDisruptionCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    IPublisher publisher) : IRequestHandler<CreateDisruptionCommand, CreateDisruptionResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IPublisher _publisher = publisher;

    public async Task<CreateDisruptionResponse> Handle(CreateDisruptionCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var flight = await _context.Flights
            .FirstOrDefaultAsync(f => f.Id == request.FlightId, cancellationToken)
            ?? throw new NotFoundException(nameof(Flight), request.FlightId);

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new NotFoundException("AirportConfig", organizationId);

        var disruption = new Disruption
        {
            OrganizationId = organizationId,
            AirportId = airport.Id,
            FlightId = request.FlightId,
            Type = request.Type,
            DetailsJson = request.DetailsJson ?? "{}",
            ReportedBy = _currentUserService.UserId,
            ReportedAt = DateTime.UtcNow,
            Status = DisruptionStatus.Active,
        };

        _context.Disruptions.Add(disruption);
        await _context.SaveChangesAsync(cancellationToken);

        // Fire-and-forget: cascade + LLM + SignalR run in background
        _ = _publisher.Publish(new DisruptionCreatedNotification(disruption.Id, organizationId), CancellationToken.None);

        return new CreateDisruptionResponse(
            disruption.Id,
            disruption.FlightId,
            flight.FlightNumber,
            disruption.Type,
            disruption.DetailsJson,
            disruption.ReportedBy,
            disruption.ReportedAt
        );
    }
}
