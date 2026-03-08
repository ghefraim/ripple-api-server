using Application.Domain.Enums;

namespace Application.Common.Interfaces;

public record CascadeContextFlight(
    Guid Id,
    string FlightNumber,
    string? Airline,
    string? Origin,
    string? Destination,
    FlightDirection Direction,
    FlightType FlightType,
    FlightStatus Status,
    DateTime ScheduledTime,
    DateTime? EstimatedTime,
    string? GateCode,
    string? CrewName,
    Guid? TurnaroundPairId
);

public record CascadeContextTimeSlot(DateTime Start, DateTime End);

public record CascadeContextImpact(
    Guid AffectedFlightId,
    string AffectedFlightNumber,
    CascadeImpactType ImpactType,
    Severity Severity,
    string Details,
    DateTime? AffectedFlightScheduledTime = null,
    DateTime? AffectedFlightEstimatedTime = null,
    string? CurrentGateCode = null
);

public record CascadeContextGate(
    Guid Id,
    string Code,
    GateType Type,
    GateSizeCategory SizeCategory,
    bool IsActive,
    List<CascadeContextTimeSlot> OccupiedSlots
);

public record CascadeContextCrew(
    Guid Id,
    string Name,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    CrewStatus Status,
    List<CascadeContextTimeSlot> OccupiedSlots
);

public record CascadeContext(
    CascadeContextFlight DisruptedFlight,
    DisruptionType DisruptionType,
    string DisruptionDetails,
    List<CascadeContextImpact> CascadeImpacts,
    List<CascadeContextGate> AvailableGates,
    List<CascadeContextCrew> AvailableCrews,
    List<string> RuleRecommendations
);

public record ActionData(
    string ActionType,
    Guid? FlightId = null,
    string? FlightNumber = null,
    Guid? TargetGateId = null,
    string? TargetGateCode = null,
    Guid? TargetCrewId = null,
    string? TargetCrewName = null
);

public record ActionPlanAction(
    int Priority,
    string Description,
    string Reasoning,
    string? SuggestedAssignee,
    string? ExecutionType = "sequential",
    List<int>? DependsOn = null,
    string? TimeTarget = null,
    string? Status = "pending",
    ActionData? ActionData = null
);

public record ActionPlanResult(
    string RawText,
    List<ActionPlanAction> Actions
);

public record RuleParseResult(
    bool Success,
    string? RuleJson,
    string? ErrorMessage
);

public interface ILlmProvider
{
    Task<ActionPlanResult> GenerateActionPlanAsync(CascadeContext context, CancellationToken cancellationToken = default);
    Task<RuleParseResult> ParseRuleAsync(string naturalLanguageInput, CancellationToken cancellationToken = default);
}
