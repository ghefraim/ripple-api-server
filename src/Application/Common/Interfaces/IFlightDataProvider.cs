using Application.Domain.Enums;

namespace Application.Common.Interfaces;

public record FlightScheduleDto(
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
    DateTime? ActualTime,
    Guid? GateId,
    string? GateCode,
    Guid? CrewId,
    string? CrewName,
    Guid? TurnaroundPairId
);

public record FlightStatusDto(
    Guid Id,
    string FlightNumber,
    FlightStatus Status,
    DateTime ScheduledTime,
    DateTime? EstimatedTime,
    DateTime? ActualTime
);

public interface IFlightDataProvider
{
    Task<List<FlightScheduleDto>> GetFlightScheduleAsync(string airportCode, DateOnly date, CancellationToken cancellationToken = default);
    Task<FlightStatusDto?> GetFlightStatusAsync(string flightNumber, CancellationToken cancellationToken = default);
}
