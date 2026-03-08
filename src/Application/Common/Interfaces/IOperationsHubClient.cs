using Application.Domain.Enums;

namespace Application.Common.Interfaces;

public interface IOperationsHubClient
{
    Task DisruptionReported(DisruptionReportedEvent data);
    Task CascadeComputed(CascadeComputedEvent data);
    Task ActionPlanGenerated(ActionPlanGeneratedEvent data);
    Task ActionPlanUpdated(ActionPlanUpdatedEvent data);
}

public record DisruptionReportedEvent(
    Guid DisruptionId,
    Guid FlightId,
    string FlightNumber,
    DisruptionType Type,
    string DetailsJson,
    DateTime ReportedAt);

public record CascadeComputedEvent(
    Guid DisruptionId,
    List<CascadeImpactDto> Impacts);

public record CascadeImpactDto(
    Guid Id,
    Guid AffectedFlightId,
    string AffectedFlightNumber,
    CascadeImpactType ImpactType,
    Severity Severity,
    string DetailsJson);

public record ActionPlanGeneratedEvent(
    Guid DisruptionId,
    Guid ActionPlanId,
    string LlmOutputText,
    string ActionsJson,
    DateTime GeneratedAt);

public record ActionPlanUpdatedEvent(
    Guid DisruptionId,
    Guid ActionPlanId,
    string ActionsJson);
