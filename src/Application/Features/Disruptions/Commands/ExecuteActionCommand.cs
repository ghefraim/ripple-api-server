using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Disruptions.Commands;

[Authorize]
public record ExecuteActionCommand(Guid DisruptionId, int ActionIndex) : IRequest<ExecuteActionResponse>;

public record ExecuteActionResponse(bool Success, string? Message, string? Error);

public class ExecuteActionCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    IOperationsNotifier notifier,
    ITelegramNotifier telegramNotifier,
    ILogger<ExecuteActionCommandHandler> logger) : IRequestHandler<ExecuteActionCommand, ExecuteActionResponse>
{
    public async Task<ExecuteActionResponse> Handle(ExecuteActionCommand request, CancellationToken cancellationToken)
    {
        var organizationId = currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var actionPlan = await context.ActionPlans
            .FirstOrDefaultAsync(ap => ap.DisruptionId == request.DisruptionId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("ActionPlan", request.DisruptionId);

        using var doc = JsonDocument.Parse(actionPlan.ActionsJson);
        var actions = doc.RootElement.EnumerateArray().ToList();

        if (request.ActionIndex < 0 || request.ActionIndex >= actions.Count)
            return new ExecuteActionResponse(false, null, "Invalid action index.");

        var action = actions[request.ActionIndex];

        if (!action.TryGetProperty("actionData", out var actionDataEl) || actionDataEl.ValueKind == JsonValueKind.Null)
            return new ExecuteActionResponse(false, null, "Action has no execution data.");

        var actionData = JsonSerializer.Deserialize<ActionDataRecord>(actionDataEl.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (actionData == null)
            return new ExecuteActionResponse(false, null, "Failed to parse action data.");

        // Execute the action and collect notification info (don't send yet — avoid concurrent DbContext access)
        var (result, notifications) = actionData.ActionType switch
        {
            "gate_reassign" => await ExecuteGateReassign(actionData, organizationId, cancellationToken),
            "crew_reassign" => await ExecuteCrewReassign(actionData, organizationId, cancellationToken),
            "notify" or "monitor" => (new ExecuteActionResponse(true, $"Action acknowledged: {GetDescription(action)}", null), new List<PendingNotification>()),
            _ => (new ExecuteActionResponse(false, null, $"Unknown action type: {actionData.ActionType}"), new List<PendingNotification>())
        };

        if (result.Success)
        {
            // Rebuild JSON with status set to "done"
            var actionsList = new List<Dictionary<string, object?>>();
            for (int i = 0; i < actions.Count; i++)
            {
                var a = actions[i];
                var dict = new Dictionary<string, object?>();
                foreach (var prop in a.EnumerateObject())
                {
                    if (prop.Name == "status" && i == request.ActionIndex)
                        continue;
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => JsonSerializer.Deserialize<object>(prop.Value.GetRawText())
                    };
                }
                if (i == request.ActionIndex)
                    dict["status"] = "done";
                else if (!dict.ContainsKey("status"))
                    dict["status"] = "pending";
                actionsList.Add(dict);
            }

            var updatedJson = JsonSerializer.Serialize(actionsList, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            actionPlan.ActionsJson = updatedJson;
            await context.SaveChangesAsync(cancellationToken);

            await notifier.NotifyActionPlanUpdated(organizationId, new ActionPlanUpdatedEvent(
                actionPlan.DisruptionId,
                actionPlan.Id,
                actionPlan.ActionsJson
            ));

            // Send Telegram notifications AFTER save to avoid concurrent DbContext access
            foreach (var n in notifications)
            {
                try
                {
                    await telegramNotifier.SendToGroupAsync(n.CrewName, organizationId, n.Message, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send Telegram notification to crew '{Crew}'", n.CrewName);
                }
            }
        }

        return result;
    }

    private record PendingNotification(string CrewName, string Message);

    private async Task<(ExecuteActionResponse, List<PendingNotification>)> ExecuteGateReassign(ActionDataRecord actionData, Guid organizationId, CancellationToken ct)
    {
        var notifications = new List<PendingNotification>();

        if (actionData.FlightId == null || actionData.TargetGateId == null)
            return (new ExecuteActionResponse(false, null, "Missing flight or gate ID."), notifications);

        var flight = await context.Flights
            .Include(f => f.Gate)
            .Include(f => f.Crew)
            .FirstOrDefaultAsync(f => f.Id == actionData.FlightId.Value, ct);

        if (flight == null)
            return (new ExecuteActionResponse(false, null, "Flight not found."), notifications);

        var gate = await context.Gates
            .FirstOrDefaultAsync(g => g.Id == actionData.TargetGateId.Value && g.IsActive, ct);

        if (gate == null)
            return (new ExecuteActionResponse(false, null, "Target gate not found or inactive."), notifications);

        var oldGateCode = flight.Gate?.Code;
        flight.GateId = gate.Id;

        logger.LogInformation("Reassigned flight {FlightNumber} from gate {OldGate} to gate {NewGate}",
            actionData.FlightNumber, oldGateCode, gate.Code);

        if (telegramNotifier.IsConfigured && flight.Crew != null)
        {
            var time = flight.EstimatedTime ?? flight.ScheduledTime;
            var timeStr = time.ToString("HH:mm");
            notifications.Add(new PendingNotification(flight.Crew.Name,
                $"\U0001f6eb <b>GATE CHANGE</b>\n" +
                $"Flight <b>{flight.FlightNumber}</b> at {timeStr}\n" +
                $"Gate changed: <b>{oldGateCode ?? "N/A"}</b> \u2192 <b>{gate.Code}</b>"));
        }

        return (new ExecuteActionResponse(true, $"Reassigned {actionData.FlightNumber} to gate {gate.Code}", null), notifications);
    }

    private async Task<(ExecuteActionResponse, List<PendingNotification>)> ExecuteCrewReassign(ActionDataRecord actionData, Guid organizationId, CancellationToken ct)
    {
        var notifications = new List<PendingNotification>();

        if (actionData.FlightId == null || actionData.TargetCrewId == null)
            return (new ExecuteActionResponse(false, null, "Missing flight or crew ID."), notifications);

        var flight = await context.Flights
            .Include(f => f.Crew)
            .FirstOrDefaultAsync(f => f.Id == actionData.FlightId.Value, ct);

        if (flight == null)
            return (new ExecuteActionResponse(false, null, "Flight not found."), notifications);

        var crew = await context.GroundCrews
            .FirstOrDefaultAsync(c => c.Id == actionData.TargetCrewId.Value, ct);

        if (crew == null)
            return (new ExecuteActionResponse(false, null, "Target crew not found."), notifications);

        var oldCrewName = flight.Crew?.Name;
        flight.CrewId = crew.Id;

        logger.LogInformation("Assigned crew {CrewName} to flight {FlightNumber}",
            crew.Name, actionData.FlightNumber);

        if (telegramNotifier.IsConfigured)
        {
            var time = flight.EstimatedTime ?? flight.ScheduledTime;
            var timeStr = time.ToString("HH:mm");

            notifications.Add(new PendingNotification(crew.Name,
                $"\u2705 <b>NEW ASSIGNMENT</b>\n" +
                $"Flight <b>{flight.FlightNumber}</b> at {timeStr}\n" +
                $"Your crew has been assigned to handle this flight."));

            if (oldCrewName != null)
            {
                notifications.Add(new PendingNotification(oldCrewName,
                    $"\u2139\ufe0f <b>REASSIGNMENT</b>\n" +
                    $"Flight <b>{flight.FlightNumber}</b> at {timeStr}\n" +
                    $"This flight has been reassigned to crew <b>{crew.Name}</b>."));
            }
        }

        return (new ExecuteActionResponse(true, $"Assigned crew {crew.Name} to {actionData.FlightNumber}", null), notifications);
    }

    private static string GetDescription(JsonElement action)
    {
        return action.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
    }

    private class ActionDataRecord
    {
        public string ActionType { get; set; } = "";
        public Guid? FlightId { get; set; }
        public string? FlightNumber { get; set; }
        public Guid? TargetGateId { get; set; }
        public string? TargetGateCode { get; set; }
        public Guid? TargetCrewId { get; set; }
        public string? TargetCrewName { get; set; }
    }
}
