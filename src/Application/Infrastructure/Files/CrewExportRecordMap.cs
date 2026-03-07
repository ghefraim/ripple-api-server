using Application.Features.Crew.ExportCrews;
using CsvHelper.Configuration;

namespace Application.Infrastructure.Files;

public class CrewExportRecordMap : ClassMap<CrewExportRecord>
{
    public CrewExportRecordMap()
    {
        Map(m => m.Name).Name("Name");
        Map(m => m.ShiftStart).Name("ShiftStart");
        Map(m => m.ShiftEnd).Name("ShiftEnd");
    }
}
