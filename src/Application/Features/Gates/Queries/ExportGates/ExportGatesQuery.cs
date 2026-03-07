using Application.Common.Security;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Gates.ExportGates;

[Authorize]
public record ExportGatesQuery() : IRequest<List<GateExportRecord>>;

public class ExportGatesQueryHandler(ApplicationDbContext context)
    : IRequestHandler<ExportGatesQuery, List<GateExportRecord>>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<GateExportRecord>> Handle(ExportGatesQuery request, CancellationToken cancellationToken)
    {
        return await _context.Gates
            .OrderBy(g => g.Code)
            .Select(g => new GateExportRecord(
                g.Code,
                g.GateType.ToString(),
                g.SizeCategory.ToString()
            ))
            .ToListAsync(cancellationToken);
    }
}
