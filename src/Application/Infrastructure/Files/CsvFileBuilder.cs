using System.Globalization;

using Application.Common.Interfaces;
using Application.Domain.Entities;
using Application.Features.Gates.ExportGates;
using Application.Features.Crew.ExportCrews;
using Application.Features.Flights.ExportFlights;

using CsvHelper;

namespace Application.Infrastructure.Files;

public class CsvFileBuilder : ICsvFileBuilder
{
    public byte[] BuildTodoItemsFile(IEnumerable<TodoItemRecord> records)
    {
        using var memoryStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(memoryStream))
        {
            using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

            csvWriter.Configuration.RegisterClassMap<TodoItemRecordMap>();
            csvWriter.WriteRecords(records);
        }

        return memoryStream.ToArray();
    }

    public byte[] BuildGatesFile(IEnumerable<GateExportRecord> records)
    {
        using var memoryStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(memoryStream))
        {
            using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

            csvWriter.Configuration.RegisterClassMap<GateExportRecordMap>();
            csvWriter.WriteRecords(records);
        }

        return memoryStream.ToArray();
    }

    public byte[] BuildCrewsFile(IEnumerable<CrewExportRecord> records)
    {
        using var memoryStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(memoryStream))
        {
            using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

            csvWriter.Configuration.RegisterClassMap<CrewExportRecordMap>();
            csvWriter.WriteRecords(records);
        }

        return memoryStream.ToArray();
    }

    public byte[] BuildFlightsFile(IEnumerable<FlightExportRecord> records)
    {
        using var memoryStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(memoryStream))
        {
            using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

            csvWriter.Configuration.RegisterClassMap<FlightExportRecordMap>();
            csvWriter.WriteRecords(records);
        }

        return memoryStream.ToArray();
    }
}