using Application.Features.Flights.ExportFlights;
using CsvHelper.Configuration;

namespace Application.Infrastructure.Files;

public class FlightExportRecordMap : ClassMap<FlightExportRecord>
{
    public FlightExportRecordMap()
    {
        Map(m => m.FlightNumber).Name("FlightNumber");
        Map(m => m.Airline).Name("Airline");
        Map(m => m.Direction).Name("Direction");
        Map(m => m.Origin).Name("Origin");
        Map(m => m.Destination).Name("Destination");
        Map(m => m.ScheduledTime).Name("ScheduledTime");
        Map(m => m.GateCode).Name("GateCode");
        Map(m => m.CrewName).Name("CrewName");
        Map(m => m.FlightType).Name("FlightType");
    }
}
