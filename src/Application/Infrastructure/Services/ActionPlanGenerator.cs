using System.Text;
using System.Text.Json;

using Application.Common.Interfaces;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;

using Microsoft.Extensions.Logging;

namespace Application.Infrastructure.Services;

public class ActionPlanGenerator : IActionPlanGenerator
{
    private readonly ILlmProvider _llmProvider;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ActionPlanGenerator> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public ActionPlanGenerator(
        ILlmProvider llmProvider,
        ApplicationDbContext context,
        ILogger<ActionPlanGenerator> logger)
    {
        _llmProvider = llmProvider;
        _context = context;
        _logger = logger;
    }

    public async Task<ActionPlan> GenerateAsync(Disruption disruption, CascadeResult cascadeResult, bool useLlm = true, CancellationToken cancellationToken = default)
    {
        ActionPlanResult result;

        if (!useLlm)
        {
            _logger.LogInformation("LLM disabled for disruption {DisruptionId}, generating fallback action plan", disruption.Id);
            result = GenerateFallbackPlan(cascadeResult);
        }
        else try
        {
            result = await _llmProvider.GenerateActionPlanAsync(cascadeResult.Context, cancellationToken);

            if (result.Actions.Count == 0 && result.RawText == "LLM call failed")
            {
                _logger.LogWarning("LLM returned failure for disruption {DisruptionId}, generating fallback", disruption.Id);
                result = GenerateFallbackPlan(cascadeResult);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM action plan generation failed for disruption {DisruptionId}, generating fallback", disruption.Id);
            result = GenerateFallbackPlan(cascadeResult);
        }

        var actionPlan = new ActionPlan
        {
            OrganizationId = disruption.OrganizationId,
            DisruptionId = disruption.Id,
            LlmOutputText = result.RawText,
            ActionsJson = JsonSerializer.Serialize(result.Actions, JsonOptions),
            GeneratedAt = DateTime.UtcNow,
        };

        _context.ActionPlans.Add(actionPlan);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Action plan generated for disruption {DisruptionId} with {ActionCount} actions",
            disruption.Id, result.Actions.Count);

        return actionPlan;
    }

    private static ActionPlanResult GenerateFallbackPlan(CascadeResult cascadeResult)
    {
        var context = cascadeResult.Context;
        var actions = new List<ActionPlanAction>();
        var priority = 1;

        // Sort impacts by severity (Critical first) then by type
        var sortedImpacts = context.CascadeImpacts
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.ImpactType)
            .ToList();

        foreach (var impact in sortedImpacts)
        {
            var (description, reasoning) = BuildFallbackAction(impact, context);
            var currentPriority = priority++;

            actions.Add(new ActionPlanAction(
                Priority: currentPriority,
                Description: description,
                Reasoning: reasoning,
                SuggestedAssignee: GetSuggestedAssignee(impact.ImpactType),
                ExecutionType: "sequential",
                DependsOn: currentPriority > 1 ? new List<int> { currentPriority - 1 } : new List<int>(),
                TimeTarget: GetTimeTarget(impact.Severity),
                Status: "pending"
            ));
        }

        // Add rule recommendations as additional actions
        foreach (var rec in context.RuleRecommendations)
        {
            var currentPriority = priority++;

            actions.Add(new ActionPlanAction(
                Priority: currentPriority,
                Description: rec,
                Reasoning: "Triggered by operational rule",
                SuggestedAssignee: null,
                ExecutionType: "sequential",
                DependsOn: currentPriority > 1 ? new List<int> { currentPriority - 1 } : new List<int>(),
                TimeTarget: "Within 15 min",
                Status: "pending"
            ));
        }

        if (actions.Count == 0)
        {
            actions.Add(new ActionPlanAction(
                Priority: 1,
                Description: "Monitor situation — no immediate cascade impacts detected",
                Reasoning: $"Disruption reported on flight {context.DisruptedFlight.FlightNumber} but no downstream impacts were identified",
                SuggestedAssignee: "Duty Manager",
                ExecutionType: "sequential",
                DependsOn: new List<int>(),
                TimeTarget: "Within 15 min",
                Status: "pending"
            ));
        }

        var rawText = BuildFallbackText(actions);
        return new ActionPlanResult(rawText, actions);
    }

    private static (string Description, string Reasoning) BuildFallbackAction(CascadeContextImpact impact, CascadeContext context)
    {
        return impact.ImpactType switch
        {
            CascadeImpactType.GateConflict => (
                $"Resolve gate conflict for flight {impact.AffectedFlightNumber} — consider reassigning to an available gate",
                $"[{impact.Severity}] Gate conflict detected: {impact.Details}"
            ),
            CascadeImpactType.TurnaroundBreach => (
                $"Address turnaround breach for flight {impact.AffectedFlightNumber} — evaluate gate reassignment or departure delay",
                $"[{impact.Severity}] Turnaround time violation: {impact.Details}"
            ),
            CascadeImpactType.CrewGap => (
                $"Resolve crew gap for flight {impact.AffectedFlightNumber} — reassign available crew",
                $"[{impact.Severity}] Crew scheduling conflict: {impact.Details}"
            ),
            CascadeImpactType.DownstreamDelay => (
                $"Monitor downstream delay for flight {impact.AffectedFlightNumber}",
                $"[{impact.Severity}] Downstream delay impact: {impact.Details}"
            ),
            _ => (
                $"Review impact on flight {impact.AffectedFlightNumber}",
                $"[{impact.Severity}] {impact.ImpactType}: {impact.Details}"
            )
        };
    }

    private static string? GetSuggestedAssignee(CascadeImpactType impactType)
    {
        return impactType switch
        {
            CascadeImpactType.GateConflict => "Gate Operations",
            CascadeImpactType.TurnaroundBreach => "Gate Operations",
            CascadeImpactType.CrewGap => "Crew Coordinator",
            CascadeImpactType.DownstreamDelay => "Duty Manager",
            _ => "Duty Manager"
        };
    }

    private static string GetTimeTarget(Severity severity)
    {
        return severity switch
        {
            Severity.Critical => "Immediate",
            Severity.Warning => "Within 15 min",
            Severity.Info => "Before departure",
            _ => "Within 15 min"
        };
    }

    private static string BuildFallbackText(List<ActionPlanAction> actions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Fallback Action Plan — LLM unavailable]");
        sb.AppendLine();
        foreach (var action in actions)
        {
            sb.AppendLine($"{action.Priority}. {action.Description}");
            sb.AppendLine($"   Reasoning: {action.Reasoning}");
            if (action.SuggestedAssignee != null)
                sb.AppendLine($"   Assignee: {action.SuggestedAssignee}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
