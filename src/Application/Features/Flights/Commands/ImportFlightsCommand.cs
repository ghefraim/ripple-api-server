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

namespace Application.Features.Flights.ImportFlights;

[Authorize(Roles = "Owner")]
public record ImportFlightsCommand(
    IFormFile File,
    DateTime OperationalDate
) : IRequest<ImportResultResponse>;

public record ImportFlightItem(
    string FlightNumber,
    string Airline,
    string Direction,
    string Origin,
    string Destination,
    string ScheduledTime,
    string? GateCode,
    string? CrewName,
    string FlightType
);

public class ImportFlightsCommandValidator : AbstractValidator<ImportFlightsCommand>
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".xlsx", ".xls" };

    public ImportFlightsCommandValidator()
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

public class ImportFlightsCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<ImportFlightsCommand, ImportResultResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<ImportResultResponse> Handle(ImportFlightsCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new NotFoundException("AirportConfig", organizationId);

        var headerAliases = new Dictionary<string, string[]>
        {
            { "flightNumber", new[] { "FlightNumber", "Flight Number", "Flight" } },
            { "airline", new[] { "Airline" } },
            { "direction", new[] { "Direction" } },
            { "origin", new[] { "Origin" } },
            { "destination", new[] { "Destination" } },
            { "scheduledTime", new[] { "ScheduledTime", "Scheduled Time", "Time" } },
            { "gateCode", new[] { "GateCode", "Gate Code", "Gate" } },
            { "crewName", new[] { "CrewName", "Crew Name", "Crew" } },
            { "flightType", new[] { "FlightType", "Flight Type" } }
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

        var items = rows.Select(row => new ImportFlightItem(
            row.GetValueOrDefault("flightNumber", ""),
            row.GetValueOrDefault("airline", ""),
            row.GetValueOrDefault("direction", ""),
            row.GetValueOrDefault("origin", ""),
            row.GetValueOrDefault("destination", ""),
            row.GetValueOrDefault("scheduledTime", ""),
            string.IsNullOrWhiteSpace(row.GetValueOrDefault("gateCode", "")) ? null : row["gateCode"],
            string.IsNullOrWhiteSpace(row.GetValueOrDefault("crewName", "")) ? null : row["crewName"],
            row.GetValueOrDefault("flightType", "")
        )).ToList();

        var gateLookup = await _context.Gates
            .Where(g => g.OrganizationId == organizationId && g.IsActive)
            .ToDictionaryAsync(g => g.Code.ToLower(), g => g.Id, cancellationToken);

        var crewLookup = await _context.GroundCrews
            .Where(c => c.OrganizationId == organizationId)
            .ToDictionaryAsync(c => c.Name.ToLower(), c => c.Id, cancellationToken);

        var operationalDate = request.OperationalDate.Date;
        var errors = new List<ImportRowError>();
        var flightsToAdd = new List<Flight>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var rowNum = i + 1;
            var hasError = false;

            if (string.IsNullOrWhiteSpace(item.FlightNumber))
            {
                errors.Add(new ImportRowError(rowNum, "FlightNumber", "Flight number is required."));
                hasError = true;
            }
            else if (item.FlightNumber.Length > 20)
            {
                errors.Add(new ImportRowError(rowNum, "FlightNumber", "Flight number must not exceed 20 characters."));
                hasError = true;
            }

            if (!Enum.TryParse<FlightDirection>(item.Direction, true, out var direction))
            {
                errors.Add(new ImportRowError(rowNum, "Direction", $"Invalid direction '{item.Direction}'. Use Arrival or Departure."));
                hasError = true;
            }

            if (!Enum.TryParse<FlightType>(item.FlightType, true, out var flightType))
            {
                errors.Add(new ImportRowError(rowNum, "FlightType", $"Invalid flight type '{item.FlightType}'. Use Domestic or International."));
                hasError = true;
            }

            if (!TimeOnly.TryParse(item.ScheduledTime, out var scheduledTime))
            {
                errors.Add(new ImportRowError(rowNum, "ScheduledTime", $"Invalid scheduled time '{item.ScheduledTime}'. Use HH:mm format."));
                hasError = true;
            }

            Guid? gateId = null;
            if (!string.IsNullOrWhiteSpace(item.GateCode))
            {
                if (!gateLookup.TryGetValue(item.GateCode.Trim().ToLower(), out var resolvedGateId))
                {
                    errors.Add(new ImportRowError(rowNum, "GateCode", $"Gate '{item.GateCode}' not found in system."));
                    hasError = true;
                }
                else
                {
                    gateId = resolvedGateId;
                }
            }

            Guid? crewId = null;
            if (!string.IsNullOrWhiteSpace(item.CrewName))
            {
                if (!crewLookup.TryGetValue(item.CrewName.Trim().ToLower(), out var resolvedCrewId))
                {
                    errors.Add(new ImportRowError(rowNum, "CrewName", $"Crew '{item.CrewName}' not found in system."));
                    hasError = true;
                }
                else
                {
                    crewId = resolvedCrewId;
                }
            }

            if (hasError) continue;

            var scheduledDateTime = operationalDate.Add(scheduledTime.ToTimeSpan());

            flightsToAdd.Add(new Flight
            {
                OrganizationId = organizationId,
                AirportId = airport.Id,
                FlightNumber = item.FlightNumber.Trim(),
                Airline = item.Airline?.Trim(),
                Origin = item.Origin?.Trim(),
                Destination = item.Destination?.Trim(),
                Direction = direction,
                FlightType = flightType,
                Status = FlightStatus.OnTime,
                ScheduledTime = scheduledDateTime,
                GateId = gateId,
                CrewId = crewId,
            });
        }

        if (flightsToAdd.Count > 0)
        {
            _context.Flights.AddRange(flightsToAdd);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ImportResultResponse(
            flightsToAdd.Count,
            items.Count - flightsToAdd.Count,
            errors);
    }
}
