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

        // Track slots claimed by earlier actions so we don't double-assign
        var claimedGateSlots = new Dictionary<Guid, List<CascadeContextTimeSlot>>();
        var claimedCrewSlots = new Dictionary<Guid, List<CascadeContextTimeSlot>>();

        // Sort impacts by severity (Critical first) then by type
        var sortedImpacts = context.CascadeImpacts
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.ImpactType)
            .ToList();

        foreach (var impact in sortedImpacts)
        {
            var (description, reasoning, actionData) = BuildSmartAction(impact, context, claimedGateSlots, claimedCrewSlots);
            var currentPriority = priority++;

            actions.Add(new ActionPlanAction(
                Priority: currentPriority,
                Description: description,
                Reasoning: reasoning,
                SuggestedAssignee: GetSuggestedAssignee(impact.ImpactType),
                ExecutionType: "sequential",
                DependsOn: currentPriority > 1 ? new List<int> { currentPriority - 1 } : new List<int>(),
                TimeTarget: GetTimeTarget(impact.Severity),
                Status: "pending",
                ActionData: actionData
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

    private static (string Description, string Reasoning, ActionData? ActionData) BuildSmartAction(
        CascadeContextImpact impact, CascadeContext context,
        Dictionary<Guid, List<CascadeContextTimeSlot>> claimedGateSlots,
        Dictionary<Guid, List<CascadeContextTimeSlot>> claimedCrewSlots)
    {
        var flightTime = impact.AffectedFlightEstimatedTime ?? impact.AffectedFlightScheduledTime;
        var buffer = TimeSpan.FromMinutes(30);

        switch (impact.ImpactType)
        {
            case CascadeImpactType.GateConflict:
            case CascadeImpactType.TurnaroundBreach:
            {
                var bestGate = FindAvailableGate(context.AvailableGates, flightTime, impact.CurrentGateCode, claimedGateSlots);
                if (bestGate != null && flightTime.HasValue)
                {
                    // Claim this slot so subsequent actions won't pick the same gate at the same time
                    var slot = new CascadeContextTimeSlot(flightTime.Value - buffer, flightTime.Value + buffer);
                    if (!claimedGateSlots.ContainsKey(bestGate.Id))
                        claimedGateSlots[bestGate.Id] = new List<CascadeContextTimeSlot>();
                    claimedGateSlots[bestGate.Id].Add(slot);

                    var label = impact.ImpactType == CascadeImpactType.TurnaroundBreach ? " for turnaround" : "";
                    return (
                        $"Reassign {impact.AffectedFlightNumber} to Gate {bestGate.Code}{label}",
                        $"[{impact.Severity}] {(impact.ImpactType == CascadeImpactType.GateConflict ? "Gate conflict" : "Turnaround violation")}: {impact.Details}",
                        new ActionData("gate_reassign", impact.AffectedFlightId, impact.AffectedFlightNumber, bestGate.Id, bestGate.Code)
                    );
                }
                return (
                    $"Resolve {(impact.ImpactType == CascadeImpactType.GateConflict ? "gate conflict" : "turnaround breach")} for {impact.AffectedFlightNumber} — no free gates",
                    $"[{impact.Severity}] {impact.Details}",
                    new ActionData("notify", impact.AffectedFlightId, impact.AffectedFlightNumber)
                );
            }
            case CascadeImpactType.CrewGap:
            {
                var bestCrew = FindAvailableCrew(context.AvailableCrews, flightTime, claimedCrewSlots);
                if (bestCrew != null && flightTime.HasValue)
                {
                    // Claim this slot so subsequent actions won't pick the same crew at the same time
                    var slot = new CascadeContextTimeSlot(flightTime.Value - buffer, flightTime.Value + buffer);
                    if (!claimedCrewSlots.ContainsKey(bestCrew.Id))
                        claimedCrewSlots[bestCrew.Id] = new List<CascadeContextTimeSlot>();
                    claimedCrewSlots[bestCrew.Id].Add(slot);

                    return (
                        $"Assign crew {bestCrew.Name} to handle {impact.AffectedFlightNumber}",
                        $"[{impact.Severity}] Crew scheduling conflict: {impact.Details}",
                        new ActionData("crew_reassign", impact.AffectedFlightId, impact.AffectedFlightNumber, TargetCrewId: bestCrew.Id, TargetCrewName: bestCrew.Name)
                    );
                }
                return (
                    $"Resolve crew gap for {impact.AffectedFlightNumber} — no crews available",
                    $"[{impact.Severity}] Crew scheduling conflict: {impact.Details}",
                    new ActionData("notify", impact.AffectedFlightId, impact.AffectedFlightNumber)
                );
            }
            case CascadeImpactType.DownstreamDelay:
                return (
                    $"Monitor downstream delay on {impact.AffectedFlightNumber}",
                    $"[{impact.Severity}] Downstream delay impact: {impact.Details}",
                    new ActionData("monitor", impact.AffectedFlightId, impact.AffectedFlightNumber)
                );
            default:
                return (
                    $"Review impact on {impact.AffectedFlightNumber}",
                    $"[{impact.Severity}] {impact.ImpactType}: {impact.Details}",
                    new ActionData("notify", impact.AffectedFlightId, impact.AffectedFlightNumber)
                );
        }
    }

    private static bool HasTimeConflict(DateTime flightTime, List<CascadeContextTimeSlot> slots)
    {
        return slots.Any(s => flightTime >= s.Start && flightTime < s.End);
    }

    private static CascadeContextGate? FindAvailableGate(
        List<CascadeContextGate> gates, DateTime? flightTime, string? currentGateCode,
        Dictionary<Guid, List<CascadeContextTimeSlot>> claimedSlots)
    {
        return gates.FirstOrDefault(g =>
            g.IsActive
            && g.Code != currentGateCode
            && (flightTime == null || !g.OccupiedSlots.Any(s => flightTime.Value >= s.Start && flightTime.Value < s.End))
            && (flightTime == null || !claimedSlots.TryGetValue(g.Id, out var claimed) || !HasTimeConflict(flightTime.Value, claimed)));
    }

    private static CascadeContextCrew? FindAvailableCrew(
        List<CascadeContextCrew> crews, DateTime? flightTime,
        Dictionary<Guid, List<CascadeContextTimeSlot>> claimedSlots)
    {
        return crews.FirstOrDefault(c =>
            c.Status == CrewStatus.Available
            && (flightTime == null || IsInShift(TimeOnly.FromDateTime(flightTime.Value), c.ShiftStart, c.ShiftEnd))
            && (flightTime == null || !c.OccupiedSlots.Any(s => flightTime.Value >= s.Start && flightTime.Value < s.End))
            && (flightTime == null || !claimedSlots.TryGetValue(c.Id, out var claimed) || !HasTimeConflict(flightTime.Value, claimed)));
    }

    private static bool IsInShift(TimeOnly time, TimeOnly shiftStart, TimeOnly shiftEnd)
    {
        return shiftEnd > shiftStart
            ? time >= shiftStart && time <= shiftEnd
            : time >= shiftStart || time <= shiftEnd;
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
