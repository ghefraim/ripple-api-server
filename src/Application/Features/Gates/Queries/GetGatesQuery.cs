using Application.Common.Security;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Gates.GetGates;

[Authorize]
public record GetGatesQuery(DateTime? Date = null) : IRequest<List<GateResponse>>;

public record GateResponse(
    Guid Id,
    string Code,
    GateType Type,
    GateSizeCategory SizeCategory,
    bool IsActive,
    List<GateFlightResponse> Flights
);

public record GateFlightResponse(
    Guid Id,
    string FlightNumber,
    DateTime ScheduledTime,
    DateTime? EstimatedTime,
    FlightStatus Status,
    FlightDirection Type
);

public class GetGatesQueryHandler(ApplicationDbContext context)
    : IRequestHandler<GetGatesQuery, List<GateResponse>>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<GateResponse>> Handle(GetGatesQuery request, CancellationToken cancellationToken)
    {
        var today = request.Date?.Date ?? DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        return await _context.Gates
            .OrderBy(g => g.Code)
            .Select(g => new GateResponse(
                g.Id,
                g.Code,
                g.GateType,
                g.SizeCategory,
                g.IsActive,
                g.Flights
                    .Where(f => f.ScheduledTime >= today && f.ScheduledTime < tomorrow)
                    .OrderBy(f => f.ScheduledTime)
                    .Select(f => new GateFlightResponse(
                        f.Id,
                        f.FlightNumber,
                        f.ScheduledTime,
                        f.EstimatedTime,
                        f.Status,
                        f.Direction
                    ))
                    .ToList()
            ))
            .ToListAsync(cancellationToken);
    }
}
