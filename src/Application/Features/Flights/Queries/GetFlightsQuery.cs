using Application.Common.Security;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Flights.GetFlights;

[Authorize]
public record GetFlightsQuery(FlightStatus? Status) : IRequest<List<FlightResponse>>;

public record FlightResponse(
    Guid Id,
    string FlightNumber,
    string? Airline,
    string? Origin,
    string? Destination,
    FlightDirection Type,
    DateTime ScheduledTime,
    DateTime? EstimatedTime,
    string? Gate,
    FlightStatus Status,
    Guid? TurnaroundPairId
);

public class GetFlightsQueryHandler(ApplicationDbContext context)
    : IRequestHandler<GetFlightsQuery, List<FlightResponse>>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<FlightResponse>> Handle(GetFlightsQuery request, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var query = _context.Flights
            .Include(f => f.Gate)
            .Where(f => f.ScheduledTime >= today && f.ScheduledTime < tomorrow)
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(f => f.Status == request.Status.Value);
        }

        return await query
            .OrderBy(f => f.ScheduledTime)
            .Select(f => new FlightResponse(
                f.Id,
                f.FlightNumber,
                f.Airline,
                f.Origin,
                f.Destination,
                f.Direction,
                f.ScheduledTime,
                f.EstimatedTime,
                f.Gate != null ? f.Gate.Code : null,
                f.Status,
                f.TurnaroundPairId
            ))
            .ToListAsync(cancellationToken);
    }
}
