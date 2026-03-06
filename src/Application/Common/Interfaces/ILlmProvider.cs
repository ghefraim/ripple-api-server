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

public record CascadeContextImpact(
    Guid AffectedFlightId,
    string AffectedFlightNumber,
    CascadeImpactType ImpactType,
    Severity Severity,
    string Details
);

public record CascadeContextGate(
    Guid Id,
    string Code,
    GateType Type,
    GateSizeCategory SizeCategory,
    bool IsActive
);

public record CascadeContextCrew(
    Guid Id,
    string Name,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    CrewStatus Status
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

public record ActionPlanAction(
    int Priority,
    string Description,
    string Reasoning,
    string? SuggestedAssignee
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
