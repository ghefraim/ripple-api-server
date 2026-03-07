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
    int AssignedFlightsCount,
    List<CrewContactDto> Contacts
);

public record CrewContactDto(
    Guid Id,
    string Name,
    string PhoneNumber,
    bool TelegramLinked
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
            .Include(c => c.Contacts)
            .OrderBy(c => c.Name)
            .Select(c => new CrewResponse(
                c.Id,
                c.Name,
                c.ShiftStart,
                c.ShiftEnd,
                c.Status,
                c.AssignedFlights
                    .Count(f => f.ScheduledTime >= today && f.ScheduledTime < tomorrow),
                c.Contacts.OrderBy(ct => ct.Name).Select(ct => new CrewContactDto(
                    ct.Id,
                    ct.Name,
                    ct.PhoneNumber,
                    ct.TelegramChatId.HasValue)).ToList()
            ))
            .ToListAsync(cancellationToken);
    }
}
