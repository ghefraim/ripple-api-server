using Application.Domain.Entities;
using Application.Features.Gates.ExportGates;
using Application.Features.Crew.ExportCrews;
using Application.Features.Flights.ExportFlights;

namespace Application.Common.Interfaces;

public interface ICsvFileBuilder
{
    byte[] BuildTodoItemsFile(IEnumerable<TodoItemRecord> records);
    byte[] BuildGatesFile(IEnumerable<GateExportRecord> records);
    byte[] BuildCrewsFile(IEnumerable<CrewExportRecord> records);
    byte[] BuildFlightsFile(IEnumerable<FlightExportRecord> records);
}