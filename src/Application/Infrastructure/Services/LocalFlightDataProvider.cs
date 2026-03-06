using Application.Common.Interfaces;
using Application.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application.Infrastructure.Services;

public class LocalFlightDataProvider(ApplicationDbContext context) : IFlightDataProvider
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<FlightScheduleDto>> GetFlightScheduleAsync(string airportCode, DateOnly date, CancellationToken cancellationToken = default)
    {
        var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = startUtc.AddDays(1);

        return await _context.Flights
            .Include(f => f.Gate)
            .Include(f => f.Crew)
            .Include(f => f.Airport)
            .Where(f => f.Airport != null && f.Airport.IataCode == airportCode)
            .Where(f => f.ScheduledTime >= startUtc && f.ScheduledTime < endUtc)
            .OrderBy(f => f.ScheduledTime)
            .Select(f => new FlightScheduleDto(
                f.Id,
                f.FlightNumber,
                f.Airline,
                f.Origin,
                f.Destination,
                f.Direction,
                f.FlightType,
                f.Status,
                f.ScheduledTime,
                f.EstimatedTime,
                f.ActualTime,
                f.GateId,
                f.Gate != null ? f.Gate.Code : null,
                f.CrewId,
                f.Crew != null ? f.Crew.Name : null,
                f.TurnaroundPairId
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<FlightStatusDto?> GetFlightStatusAsync(string flightNumber, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        return await _context.Flights
            .Where(f => f.FlightNumber == flightNumber)
            .Where(f => f.ScheduledTime >= today && f.ScheduledTime < tomorrow)
            .Select(f => new FlightStatusDto(
                f.Id,
                f.FlightNumber,
                f.Status,
                f.ScheduledTime,
                f.EstimatedTime,
                f.ActualTime
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
