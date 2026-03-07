using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Common.Security;
using Application.Common.Utilities;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Gates.ImportGates;

[Authorize(Roles = "Owner")]
public record ImportGatesCommand(IFormFile File) : IRequest<ImportResultResponse>;

public record ImportGateItem(string Code, string GateType, string SizeCategory);

public class ImportGatesCommandValidator : AbstractValidator<ImportGatesCommand>
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".xlsx", ".xls" };

    public ImportGatesCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("File is required.");

        RuleFor(x => x.File.Length)
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)}MB.")
            .GreaterThan(0)
            .WithMessage("File cannot be empty.")
            .When(x => x.File != null);

        RuleFor(x => x.File.FileName)
            .Must(fileName =>
            {
                if (string.IsNullOrEmpty(fileName)) return false;
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                return AllowedExtensions.Contains(extension);
            })
            .WithMessage($"File must have one of the following extensions: {string.Join(", ", AllowedExtensions)}")
            .When(x => x.File != null);
    }
}

public class ImportGatesCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<ImportGatesCommand, ImportResultResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<ImportResultResponse> Handle(ImportGatesCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new NotFoundException("AirportConfig", organizationId);

        var headerAliases = new Dictionary<string, string[]>
        {
            { "code", new[] { "Code", "Gate Code", "GateCode" } },
            { "gateType", new[] { "GateType", "Gate Type", "Type" } },
            { "sizeCategory", new[] { "SizeCategory", "Size Category", "Size" } }
        };

        List<Dictionary<string, string>> rows;
        try
        {
            rows = ExcelParser.ParseExcelFile(request.File, headerAliases);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse Excel file: {ex.Message}", ex);
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("The file contains no data rows.");
        }

        if (rows.Count > 500)
        {
            throw new InvalidOperationException("File contains more than 500 rows. Please split into smaller files.");
        }

        var items = rows.Select(row => new ImportGateItem(
            row.GetValueOrDefault("code", ""),
            row.GetValueOrDefault("gateType", ""),
            row.GetValueOrDefault("sizeCategory", "")
        )).ToList();

        var existingCodes = (await _context.Gates
            .Where(g => g.OrganizationId == organizationId)
            .Select(g => g.Code.ToLower())
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var errors = new List<ImportRowError>();
        var gatesToAdd = new List<Gate>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var rowNum = i + 1;
            var hasError = false;

            if (string.IsNullOrWhiteSpace(item.Code))
            {
                errors.Add(new ImportRowError(rowNum, "Code", "Code is required."));
                hasError = true;
            }
            else if (item.Code.Length > 20)
            {
                errors.Add(new ImportRowError(rowNum, "Code", "Code must not exceed 20 characters."));
                hasError = true;
            }
            else if (existingCodes.Contains(item.Code.ToLower()) || !seenCodes.Add(item.Code))
            {
                errors.Add(new ImportRowError(rowNum, "Code", $"Gate code '{item.Code}' already exists."));
                hasError = true;
            }

            if (!Enum.TryParse<GateType>(item.GateType, true, out var gateType))
            {
                errors.Add(new ImportRowError(rowNum, "GateType", $"Invalid gate type '{item.GateType}'. Use Domestic, International, or Both."));
                hasError = true;
            }

            if (!Enum.TryParse<GateSizeCategory>(item.SizeCategory, true, out var sizeCategory))
            {
                errors.Add(new ImportRowError(rowNum, "SizeCategory", $"Invalid size category '{item.SizeCategory}'. Use Narrow or Wide."));
                hasError = true;
            }

            if (hasError) continue;

            gatesToAdd.Add(new Gate
            {
                OrganizationId = organizationId,
                AirportId = airport.Id,
                Code = item.Code.Trim(),
                GateType = gateType,
                SizeCategory = sizeCategory,
                IsActive = true,
            });
        }

        if (gatesToAdd.Count > 0)
        {
            _context.Gates.AddRange(gatesToAdd);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ImportResultResponse(
            gatesToAdd.Count,
            items.Count - gatesToAdd.Count,
            errors);
    }
}
