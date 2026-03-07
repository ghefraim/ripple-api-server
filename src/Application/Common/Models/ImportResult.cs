namespace Application.Common.Models;

public record ImportResultResponse(int Imported, int Skipped, List<ImportRowError> Errors);

public record ImportRowError(int Row, string Field, string Message);
