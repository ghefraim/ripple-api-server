using Application.Common.Interfaces;
using Application.Infrastructure.Persistence;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Disruptions.Events;

public class DisruptionCreatedHandler : INotificationHandler<DisruptionCreatedNotification>
{
    private readonly ApplicationDbContext _context;
    private readonly ICascadeEngine _cascadeEngine;
    private readonly IActionPlanGenerator _actionPlanGenerator;
    private readonly IOperationsNotifier _notifier;
    private readonly ILogger<DisruptionCreatedHandler> _logger;

    public DisruptionCreatedHandler(
        ApplicationDbContext context,
        ICascadeEngine cascadeEngine,
        IActionPlanGenerator actionPlanGenerator,
        IOperationsNotifier notifier,
        ILogger<DisruptionCreatedHandler> logger)
    {
        _context = context;
        _cascadeEngine = cascadeEngine;
        _actionPlanGenerator = actionPlanGenerator;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Handle(DisruptionCreatedNotification notification, CancellationToken cancellationToken)
    {
        var disruption = await _context.Disruptions
            .Include(d => d.Flight)
            .FirstOrDefaultAsync(d => d.Id == notification.DisruptionId, cancellationToken);

        if (disruption == null)
        {
            _logger.LogError("Disruption {DisruptionId} not found for pipeline processing", notification.DisruptionId);
            return;
        }

        var orgId = notification.OrganizationId;

        // 1. Broadcast DisruptionReported immediately
        await _notifier.NotifyDisruptionReported(orgId, new DisruptionReportedEvent(
            disruption.Id,
            disruption.FlightId,
            disruption.Flight.FlightNumber,
            disruption.Type,
            disruption.DetailsJson,
            disruption.ReportedAt));

        _logger.LogInformation("DisruptionReported broadcast for disruption {DisruptionId}", disruption.Id);

        // 2. Run cascade engine
        CascadeResult cascadeResult;
        try
        {
            cascadeResult = await _cascadeEngine.ProcessDisruptionAsync(disruption, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cascade engine failed for disruption {DisruptionId}", disruption.Id);
            return;
        }

        // 3. Broadcast CascadeComputed
        var impactDtos = new List<CascadeImpactDto>();
        foreach (var impact in cascadeResult.Impacts)
        {
            var affectedFlight = await _context.Flights
                .FirstOrDefaultAsync(f => f.Id == impact.AffectedFlightId, cancellationToken);

            impactDtos.Add(new CascadeImpactDto(
                impact.Id,
                impact.AffectedFlightId,
                affectedFlight?.FlightNumber ?? "Unknown",
                impact.ImpactType,
                impact.Severity,
                impact.Details));
        }

        await _notifier.NotifyCascadeComputed(orgId, new CascadeComputedEvent(
            disruption.Id,
            impactDtos));

        _logger.LogInformation("CascadeComputed broadcast for disruption {DisruptionId} with {ImpactCount} impacts",
            disruption.Id, cascadeResult.Impacts.Count);

        // 4. Generate LLM action plan
        try
        {
            var actionPlan = await _actionPlanGenerator.GenerateAsync(disruption, cascadeResult, cancellationToken);

            // 5. Broadcast ActionPlanGenerated
            await _notifier.NotifyActionPlanGenerated(orgId, new ActionPlanGeneratedEvent(
                disruption.Id,
                actionPlan.Id,
                actionPlan.LlmOutputText ?? "",
                actionPlan.ActionsJson,
                actionPlan.GeneratedAt));

            _logger.LogInformation("ActionPlanGenerated broadcast for disruption {DisruptionId}", disruption.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Action plan generation failed for disruption {DisruptionId}", disruption.Id);
        }
    }
}
