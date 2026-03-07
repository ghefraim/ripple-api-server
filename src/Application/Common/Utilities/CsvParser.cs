using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;

namespace Application.Common.Utilities;

public static class CsvParser
{
    public static List<Dictionary<string, string>> ParseCsvFile(
        IFormFile file,
        Dictionary<string, string[]> headerAliases)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        
        var firstLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            throw new InvalidOperationException("CSV file has no headers.");
        }

        var headers = firstLine.Split(',').Select(h => h.Trim(' ', '"')).ToArray();
        var headerMap = BuildHeaderMap(headers, headerAliases);

        if (headerMap.Count == 0)
        {
            throw new InvalidOperationException("No valid headers found in CSV file. Please check the column headers match the expected format.");
        }

        using var csvReader = new CsvReader(new StreamReader(file.OpenReadStream()), new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        });

        csvReader.Read();
        csvReader.ReadHeader();

        var rows = new List<Dictionary<string, string>>();

        while (csvReader.Read())
        {
            var rowData = new Dictionary<string, string>();
            var hasData = false;

            foreach (var (csvHeader, targetKey) in headerMap)
            {
                var value = csvReader.GetField(csvHeader)?.Trim() ?? string.Empty;
                rowData[targetKey] = value;

                if (!string.IsNullOrWhiteSpace(value))
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

    private static Dictionary<string, string> BuildHeaderMap(
        string[] headers,
        Dictionary<string, string[]> headerAliases)
    {
        var headerMap = new Dictionary<string, string>();

        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header))
                continue;

            var normalizedHeader = NormalizeHeader(header);

            foreach (var (targetKey, aliases) in headerAliases)
            {
                if (aliases.Any(alias => NormalizeHeader(alias) == normalizedHeader))
                {
                    headerMap[header] = targetKey;
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
}
