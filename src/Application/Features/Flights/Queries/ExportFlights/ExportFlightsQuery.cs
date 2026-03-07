using Application.Common.Security;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Flights.ExportFlights;

[Authorize]
public record ExportFlightsQuery(FlightStatus? Status = null) : IRequest<List<FlightExportRecord>>;

public class ExportFlightsQueryHandler(ApplicationDbContext context)
    : IRequestHandler<ExportFlightsQuery, List<FlightExportRecord>>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<FlightExportRecord>> Handle(ExportFlightsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Flights.AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(f => f.Status == request.Status.Value);
        }

        return await query
            .OrderBy(f => f.ScheduledTime)
            .Select(f => new FlightExportRecord(
                f.FlightNumber,
                f.Airline ?? "",
                f.Direction.ToString(),
                f.Origin ?? "",
                f.Destination ?? "",
                f.ScheduledTime.ToString("HH:mm"),
                f.Gate != null ? f.Gate.Code : "",
                f.Crew != null ? f.Crew.Name : "",
                f.FlightType.ToString()
            ))
            .ToListAsync(cancellationToken);
    }
}
