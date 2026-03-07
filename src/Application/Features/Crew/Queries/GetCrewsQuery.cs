using Application.Common.Security;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Crew.GetCrews;

[Authorize]
public record GetCrewsQuery : IRequest<List<CrewResponse>>;

public record CrewResponse(
    Guid Id,
    string Name,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    CrewStatus Status,
    int AssignedFlightsCount
);

public class GetCrewsQueryHandler(ApplicationDbContext context)
    : IRequestHandler<GetCrewsQuery, List<CrewResponse>>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<CrewResponse>> Handle(GetCrewsQuery request, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        return await _context.GroundCrews
            .OrderBy(c => c.Name)
            .Select(c => new CrewResponse(
                c.Id,
                c.Name,
                c.ShiftStart,
                c.ShiftEnd,
                c.Status,
                c.AssignedFlights
                    .Count(f => f.ScheduledTime >= today && f.ScheduledTime < tomorrow)
            ))
            .ToListAsync(cancellationToken);
    }
}
