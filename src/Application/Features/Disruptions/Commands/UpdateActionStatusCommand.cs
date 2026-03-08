using Application.Common.Interfaces;
using Application.Infrastructure.Persistence;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Application.Features.Disruptions.Commands;

public record UpdateActionStatusCommand(
    Guid DisruptionId,
    int ActionIndex,
    string Status
) : IRequest<UpdateActionStatusResponse>;

public record UpdateActionStatusResponse(
    Guid ActionPlanId,
    string ActionsJson,
    string? Error
);

public record UpdateActionStatusRequest(string Status);

public class UpdateActionStatusCommandHandler(
    ApplicationDbContext context,
    IOperationsNotifier notifier,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateActionStatusCommand, UpdateActionStatusResponse>
{
    public async Task<UpdateActionStatusResponse> Handle(UpdateActionStatusCommand request, CancellationToken cancellationToken)
    {
        var validStatuses = new[] { "pending", "in_progress", "done", "skipped" };
        if (!validStatuses.Contains(request.Status))
            throw new Application.Common.Exceptions.ValidationException(
                new[] { new ValidationFailure("Status", $"Invalid status '{request.Status}'. Must be one of: {string.Join(", ", validStatuses)}") });

        var organizationId = currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var actionPlan = await context.ActionPlans
            .FirstOrDefaultAsync(ap => ap.DisruptionId == request.DisruptionId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("ActionPlan", request.DisruptionId);

        // Parse ActionsJson, update status at index, save back
        using var doc = JsonDocument.Parse(actionPlan.ActionsJson);
        var actions = doc.RootElement.EnumerateArray().ToList();

        if (request.ActionIndex < 0 || request.ActionIndex >= actions.Count)
            throw new Application.Common.Exceptions.ValidationException(
                new[] { new ValidationFailure("ActionIndex", $"Action index {request.ActionIndex} out of range (0-{actions.Count - 1})") });

        // Rebuild the JSON array with updated status
        var actionsList = new List<Dictionary<string, object?>>();
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var dict = new Dictionary<string, object?>();
            foreach (var prop in action.EnumerateObject())
            {
                if (prop.Name == "status" && i == request.ActionIndex)
                    continue; // will add updated status below
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
                dict["status"] = request.Status;
            else if (!dict.ContainsKey("status"))
                dict["status"] = "pending";
            actionsList.Add(dict);
        }

        var updatedJson = JsonSerializer.Serialize(actionsList, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        actionPlan.ActionsJson = updatedJson;
        await context.SaveChangesAsync(cancellationToken);

        // Broadcast via SignalR
        await notifier.NotifyActionPlanUpdated(organizationId, new ActionPlanUpdatedEvent(
            actionPlan.DisruptionId,
            actionPlan.Id,
            actionPlan.ActionsJson
        ));

        return new UpdateActionStatusResponse(actionPlan.Id, updatedJson, null);
    }
}
