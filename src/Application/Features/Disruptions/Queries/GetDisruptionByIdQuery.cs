using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Disruptions.GetDisruptionById;

[Authorize]
public record GetDisruptionByIdQuery(Guid Id) : IRequest<DisruptionDetailResponse>;

public record DisruptionDetailResponse(
    Guid Id,
    Guid FlightId,
    string FlightNumber,
    DisruptionType Type,
    string DetailsJson,
    string? ReportedBy,
    DateTime ReportedAt,
    DisruptionStatus Status,
    List<CascadeImpactResponse> CascadeImpacts,
    ActionPlanResponse? ActionPlan);

public record CascadeImpactResponse(
    Guid Id,
    Guid AffectedFlightId,
    string AffectedFlightNumber,
    CascadeImpactType ImpactType,
    Severity Severity,
    string DetailsJson);

public record ActionPlanResponse(
    Guid Id,
    string? LlmOutputText,
    string ActionsJson,
    DateTime GeneratedAt);

public class GetDisruptionByIdQueryHandler(
    ApplicationDbContext context) : IRequestHandler<GetDisruptionByIdQuery, DisruptionDetailResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<DisruptionDetailResponse> Handle(GetDisruptionByIdQuery request, CancellationToken cancellationToken)
    {
        var disruption = await _context.Disruptions
            .Include(d => d.Flight)
            .Include(d => d.CascadeImpacts)
                .ThenInclude(ci => ci.AffectedFlight)
            .Include(d => d.ActionPlan)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Disruption), request.Id);

        var impacts = disruption.CascadeImpacts.Select(ci => new CascadeImpactResponse(
            ci.Id,
            ci.AffectedFlightId,
            ci.AffectedFlight.FlightNumber,
            ci.ImpactType,
            ci.Severity,
            ci.Details)).ToList();

        ActionPlanResponse? actionPlan = null;
        if (disruption.ActionPlan != null)
        {
            actionPlan = new ActionPlanResponse(
                disruption.ActionPlan.Id,
                disruption.ActionPlan.LlmOutputText,
                disruption.ActionPlan.ActionsJson,
                disruption.ActionPlan.GeneratedAt);
        }

        return new DisruptionDetailResponse(
            disruption.Id,
            disruption.FlightId,
            disruption.Flight.FlightNumber,
            disruption.Type,
            disruption.DetailsJson,
            disruption.ReportedBy,
            disruption.ReportedAt,
            disruption.Status,
            impacts,
            actionPlan);
    }
}
