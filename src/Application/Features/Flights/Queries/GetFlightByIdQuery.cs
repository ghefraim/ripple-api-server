using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Flights.GetFlightById;

[Authorize]
public record GetFlightByIdQuery(Guid Id) : IRequest<FlightDetailResponse>;

public record FlightDetailResponse(
    Guid Id,
    string FlightNumber,
    string? Airline,
    string? Origin,
    string? Destination,
    FlightDirection Type,
    FlightType FlightType,
    DateTime ScheduledTime,
    DateTime? EstimatedTime,
    DateTime? ActualTime,
    string? Gate,
    Guid? GateId,
    FlightStatus Status,
    Guid? TurnaroundPairId,
    string? CrewName,
    Guid? CrewId
);

public class GetFlightByIdQueryHandler(ApplicationDbContext context)
    : IRequestHandler<GetFlightByIdQuery, FlightDetailResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<FlightDetailResponse> Handle(GetFlightByIdQuery request, CancellationToken cancellationToken)
    {
        var flight = await _context.Flights
            .Include(f => f.Gate)
            .Include(f => f.Crew)
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Flight), request.Id);

        return new FlightDetailResponse(
            flight.Id,
            flight.FlightNumber,
            flight.Airline,
            flight.Origin,
            flight.Destination,
            flight.Direction,
            flight.FlightType,
            flight.ScheduledTime,
            flight.EstimatedTime,
            flight.ActualTime,
            flight.Gate?.Code,
            flight.GateId,
            flight.Status,
            flight.TurnaroundPairId,
            flight.Crew?.Name,
            flight.CrewId
        );
    }
}
