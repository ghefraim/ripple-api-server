using Application.Features.Gates.ExportGates;
using CsvHelper.Configuration;

namespace Application.Infrastructure.Files;

public class GateExportRecordMap : ClassMap<GateExportRecord>
{
    public GateExportRecordMap()
    {
        Map(m => m.Code).Name("Code");
        Map(m => m.GateType).Name("GateType");
        Map(m => m.SizeCategory).Name("SizeCategory");
    }
}
