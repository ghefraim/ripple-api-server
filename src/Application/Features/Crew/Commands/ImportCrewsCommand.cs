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

namespace Application.Features.Crew.ImportCrews;

[Authorize(Roles = "Owner")]
public record ImportCrewsCommand(IFormFile File) : IRequest<ImportResultResponse>;

public record ImportCrewItem(string Name, string ShiftStart, string ShiftEnd);

public class ImportCrewsCommandValidator : AbstractValidator<ImportCrewsCommand>
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".xlsx", ".xls", ".csv" };

    public ImportCrewsCommandValidator()
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

public class ImportCrewsCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<ImportCrewsCommand, ImportResultResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<ImportResultResponse> Handle(ImportCrewsCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new NotFoundException("AirportConfig", organizationId);

        var headerAliases = new Dictionary<string, string[]>
        {
            { "name", new[] { "Name", "Crew Name", "CrewName" } },
            { "shiftStart", new[] { "ShiftStart", "Shift Start", "Start" } },
            { "shiftEnd", new[] { "ShiftEnd", "Shift End", "End" } }
        };

        List<Dictionary<string, string>> rows;
        try
        {
            rows = FileParser.ParseFile(request.File, headerAliases);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse file: {ex.Message}", ex);
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("The file contains no data rows.");
        }

        if (rows.Count > 500)
        {
            throw new InvalidOperationException("File contains more than 500 rows. Please split into smaller files.");
        }

        var items = rows.Select(row => new ImportCrewItem(
            row.GetValueOrDefault("name", ""),
            row.GetValueOrDefault("shiftStart", ""),
            row.GetValueOrDefault("shiftEnd", "")
        )).ToList();

        var existingNames = (await _context.GroundCrews
            .Where(c => c.OrganizationId == organizationId)
            .Select(c => c.Name.ToLower())
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var errors = new List<ImportRowError>();
        var crewsToAdd = new List<GroundCrew>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var rowNum = i + 1;
            var hasError = false;

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                errors.Add(new ImportRowError(rowNum, "Name", "Name is required."));
                hasError = true;
            }
            else if (item.Name.Length > 100)
            {
                errors.Add(new ImportRowError(rowNum, "Name", "Name must not exceed 100 characters."));
                hasError = true;
            }
            else if (existingNames.Contains(item.Name.ToLower()) || !seenNames.Add(item.Name))
            {
                errors.Add(new ImportRowError(rowNum, "Name", $"Crew name '{item.Name}' already exists."));
                hasError = true;
            }

            if (!TimeOnly.TryParse(item.ShiftStart, out var shiftStart))
            {
                errors.Add(new ImportRowError(rowNum, "ShiftStart", $"Invalid shift start time '{item.ShiftStart}'. Use HH:mm format."));
                hasError = true;
            }

            if (!TimeOnly.TryParse(item.ShiftEnd, out var shiftEnd))
            {
                errors.Add(new ImportRowError(rowNum, "ShiftEnd", $"Invalid shift end time '{item.ShiftEnd}'. Use HH:mm format."));
                hasError = true;
            }

            if (hasError) continue;

            crewsToAdd.Add(new GroundCrew
            {
                OrganizationId = organizationId,
                AirportId = airport.Id,
                Name = item.Name.Trim(),
                ShiftStart = shiftStart,
                ShiftEnd = shiftEnd,
                Status = CrewStatus.Available,
            });
        }

        if (crewsToAdd.Count > 0)
        {
            _context.GroundCrews.AddRange(crewsToAdd);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ImportResultResponse(
            crewsToAdd.Count,
            items.Count - crewsToAdd.Count,
            errors);
    }
}
