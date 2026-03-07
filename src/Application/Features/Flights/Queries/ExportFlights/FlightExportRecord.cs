namespace Application.Features.Flights.ExportFlights;

public record FlightExportRecord(
    string FlightNumber,
    string Airline,
    string Direction,
    string Origin,
    string Destination,
    string ScheduledTime,
    string GateCode,
    string CrewName,
    string FlightType
);
