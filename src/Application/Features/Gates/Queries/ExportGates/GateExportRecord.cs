namespace Application.Features.Gates.ExportGates;

public record GateExportRecord(
    string Code,
    string GateType,
    string SizeCategory
);
