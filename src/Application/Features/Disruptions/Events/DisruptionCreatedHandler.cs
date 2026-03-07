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
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly ILogger<DisruptionCreatedHandler> _logger;

    public DisruptionCreatedHandler(
        ApplicationDbContext context,
        ICascadeEngine cascadeEngine,
        IActionPlanGenerator actionPlanGenerator,
        IOperationsNotifier notifier,
        ITelegramNotifier telegramNotifier,
        ILogger<DisruptionCreatedHandler> logger)
    {
        _context = context;
        _cascadeEngine = cascadeEngine;
        _actionPlanGenerator = actionPlanGenerator;
        _notifier = notifier;
        _telegramNotifier = telegramNotifier;
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

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == orgId, cancellationToken);
        var llmEnabled = airport?.LlmEnabled ?? false;

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
            var actionPlan = await _actionPlanGenerator.GenerateAsync(disruption, cascadeResult, llmEnabled, cancellationToken);

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

        // 6. Send Telegram notifications to employee groups targeted by rules
        if (cascadeResult.NotificationTargets.Count > 0 && _telegramNotifier.IsConfigured)
        {
            var severityEmoji = cascadeResult.Impacts.Any(i => i.Severity == Domain.Enums.Severity.Critical)
                ? "\u26a0\ufe0f" : "\u2139\ufe0f";

            var impactSummary = cascadeResult.Impacts.Count > 0
                ? string.Join("\n", impactDtos.Select(i => $"  - {i.AffectedFlightNumber}: {i.ImpactType} ({i.Severity})"))
                : "  No cascade impacts detected";

            var telegramMessage =
                $"{severityEmoji} <b>DISRUPTION ALERT</b>\n" +
                $"Flight <b>{disruption.Flight.FlightNumber}</b> - {disruption.Type}\n" +
                $"Details: {disruption.DetailsJson}\n\n" +
                $"<b>Cascade Impacts:</b>\n{impactSummary}";

            foreach (var groupName in cascadeResult.NotificationTargets)
            {
                try
                {
                    await _telegramNotifier.SendToGroupAsync(groupName, orgId, telegramMessage, cancellationToken);
                    _logger.LogInformation("Telegram notification sent to group '{Group}' for disruption {DisruptionId}",
                        groupName, disruption.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send Telegram notification to group '{Group}'", groupName);
                }
            }
        }
    }
}
