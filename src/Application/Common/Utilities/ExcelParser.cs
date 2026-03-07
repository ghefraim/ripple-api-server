using Microsoft.AspNetCore.Http;
using OfficeOpenXml;

namespace Application.Common.Utilities;

public static class ExcelParser
{
    static ExcelParser()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public static List<Dictionary<string, string>> ParseExcelFile(
        IFormFile file,
        Dictionary<string, string[]> headerAliases)
    {
        using var stream = file.OpenReadStream();
        using var package = new ExcelPackage(stream);

        var worksheet = package.Workbook.Worksheets[0];
        if (worksheet.Dimension == null)
        {
            return new List<Dictionary<string, string>>();
        }

        var rows = new List<Dictionary<string, string>>();
        var headerMap = BuildHeaderMap(worksheet, headerAliases);

        if (headerMap.Count == 0)
        {
            throw new InvalidOperationException("No valid headers found in Excel file. Please check the column headers match the expected format.");
        }

        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
        {
            var rowData = new Dictionary<string, string>();
            var hasData = false;

            foreach (var (excelCol, targetKey) in headerMap)
            {
                var cellValue = worksheet.Cells[row, excelCol].Value;
                var stringValue = ConvertCellValue(cellValue);
                rowData[targetKey] = stringValue;

                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    hasData = true;
                }
            }

            if (hasData)
            {
                rows.Add(rowData);
            }
        }

        return rows;
    }

    private static Dictionary<int, string> BuildHeaderMap(
        ExcelWorksheet worksheet,
        Dictionary<string, string[]> headerAliases)
    {
        var headerMap = new Dictionary<int, string>();

        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
        {
            var headerCell = worksheet.Cells[1, col].Value?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(headerCell))
                continue;

            var normalizedHeader = NormalizeHeader(headerCell);

            foreach (var (targetKey, aliases) in headerAliases)
            {
                if (aliases.Any(alias => NormalizeHeader(alias) == normalizedHeader))
                {
                    headerMap[col] = targetKey;
                    break;
                }
            }
        }

        return headerMap;
    }

    private static string NormalizeHeader(string header)
    {
        return header
            .Replace("\uFEFF", "")
            .Trim()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .ToLowerInvariant();
    }

    private static string ConvertCellValue(object? cellValue)
    {
        if (cellValue == null)
            return string.Empty;

        if (cellValue is DateTime dateTime)
        {
            if (dateTime.TimeOfDay.TotalSeconds > 0)
            {
                return dateTime.ToString("HH:mm");
            }
            return dateTime.ToString("yyyy-MM-dd");
        }

        if (cellValue is double doubleValue)
        {
            if (doubleValue >= 0 && doubleValue < 1)
            {
                var totalMinutes = (int)Math.Round(doubleValue * 24 * 60);
                var hours = totalMinutes / 60;
                var minutes = totalMinutes % 60;
                return $"{hours:D2}:{minutes:D2}";
            }
        }

        return cellValue.ToString()?.Trim() ?? string.Empty;
    }
}
