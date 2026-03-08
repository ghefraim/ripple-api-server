using Application.Common.Security;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disruptions.GetDisruptions;

[Authorize]
public record GetDisruptionsQuery(DateTime? Date = null, bool IncludeArchived = false) : IRequest<List<DisruptionSummaryResponse>>;

public record DisruptionSummaryResponse(
    Guid Id,
    Guid FlightId,
    string FlightNumber,
    DisruptionType Type,
    string DetailsJson,
    string? ReportedBy,
    DateTime ReportedAt,
    DisruptionStatus Status,
    int ImpactCount,
    Severity? HighestSeverity);

public class GetDisruptionsQueryHandler(
    ApplicationDbContext context) : IRequestHandler<GetDisruptionsQuery, List<DisruptionSummaryResponse>>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<DisruptionSummaryResponse>> Handle(GetDisruptionsQuery request, CancellationToken cancellationToken)
    {
        var today = request.Date?.Date ?? DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var query = _context.Disruptions
            .Include(d => d.Flight)
            .Include(d => d.CascadeImpacts)
            .Where(d => d.ReportedAt >= today && d.ReportedAt < tomorrow);

        if (!request.IncludeArchived)
            query = query.Where(d => d.Status != DisruptionStatus.Archived);

        var disruptions = await query
            .OrderByDescending(d => d.ReportedAt)
            .ToListAsync(cancellationToken);

        return disruptions.Select(d =>
        {
            var impactCount = d.CascadeImpacts.Count;
            Severity? highestSeverity = d.CascadeImpacts.Count > 0
                ? d.CascadeImpacts.Max(ci => ci.Severity)
                : null;

            return new DisruptionSummaryResponse(
                d.Id,
                d.FlightId,
                d.Flight.FlightNumber,
                d.Type,
                d.DetailsJson,
                d.ReportedBy,
                d.ReportedAt,
                d.Status,
                impactCount,
                highestSeverity);
        }).ToList();
    }
}
