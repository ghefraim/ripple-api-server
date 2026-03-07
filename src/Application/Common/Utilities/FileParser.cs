using Microsoft.AspNetCore.Http;

namespace Application.Common.Utilities;

public static class FileParser
{
    public static List<Dictionary<string, string>> ParseFile(
        IFormFile file,
        Dictionary<string, string[]> headerAliases)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        return extension switch
        {
            ".csv" => CsvParser.ParseCsvFile(file, headerAliases),
            ".xlsx" or ".xls" => ExcelParser.ParseExcelFile(file, headerAliases),
            _ => throw new InvalidOperationException($"Unsupported file format: {extension}. Please upload .xlsx, .xls, or .csv file.")
        };
    }
}
