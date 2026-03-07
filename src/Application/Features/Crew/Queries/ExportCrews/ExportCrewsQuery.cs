using Application.Common.Security;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Crew.ExportCrews;

[Authorize]
public record ExportCrewsQuery() : IRequest<List<CrewExportRecord>>;

public class ExportCrewsQueryHandler(ApplicationDbContext context)
    : IRequestHandler<ExportCrewsQuery, List<CrewExportRecord>>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<CrewExportRecord>> Handle(ExportCrewsQuery request, CancellationToken cancellationToken)
    {
        return await _context.GroundCrews
            .OrderBy(c => c.Name)
            .Select(c => new CrewExportRecord(
                c.Name,
                c.ShiftStart.ToString(@"HH\:mm"),
                c.ShiftEnd.ToString(@"HH\:mm")
            ))
            .ToListAsync(cancellationToken);
    }
}
