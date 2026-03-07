using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Crew.GetCrewById;

[Authorize]
public record GetCrewByIdQuery(Guid Id) : IRequest<CrewDetailResponse>;

public record CrewDetailResponse(
    Guid Id,
    string Name,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    CrewStatus Status,
    List<CrewFlightResponse> AssignedFlights
);

public record CrewFlightResponse(
    Guid Id,
    string FlightNumber,
    DateTime ScheduledTime,
    FlightStatus FlightStatus,
    FlightDirection Type,
    string? Gate
);

public class GetCrewByIdQueryHandler(ApplicationDbContext context)
    : IRequestHandler<GetCrewByIdQuery, CrewDetailResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<CrewDetailResponse> Handle(GetCrewByIdQuery request, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var crew = await _context.GroundCrews
            .Include(c => c.AssignedFlights.Where(f => f.ScheduledTime >= today && f.ScheduledTime < tomorrow))
                .ThenInclude(f => f.Gate)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(GroundCrew), request.Id);

        return new CrewDetailResponse(
            crew.Id,
            crew.Name,
            crew.ShiftStart,
            crew.ShiftEnd,
            crew.Status,
            crew.AssignedFlights
                .OrderBy(f => f.ScheduledTime)
                .Select(f => new CrewFlightResponse(
                    f.Id,
                    f.FlightNumber,
                    f.ScheduledTime,
                    f.Status,
                    f.Direction,
                    f.Gate?.Code
                ))
                .ToList()
        );
    }
}
