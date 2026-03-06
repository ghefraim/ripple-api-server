using System.Text.Json;

using Application.Common.Interfaces;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Infrastructure.Services;

public class CascadeEngine : ICascadeEngine
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CascadeEngine> _logger;
    private readonly int _gateBufferMinutes;

    public CascadeEngine(
        ApplicationDbContext context,
        ILogger<CascadeEngine> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _gateBufferMinutes = configuration.GetValue("CascadeEngine:GateBufferMinutes", 30);
    }

    public async Task<CascadeResult> ProcessDisruptionAsync(Disruption disruption, CancellationToken cancellationToken = default)
    {
        var flight = await _context.Flights
            .Include(f => f.Gate)
            .Include(f => f.Crew)
            .FirstOrDefaultAsync(f => f.Id == disruption.FlightId, cancellationToken)
            ?? throw new InvalidOperationException($"Flight {disruption.FlightId} not found");

        var impacts = new List<CascadeImpact>();

        ApplyDirectImpact(flight, disruption);

        var gateConflicts = await DetectGateConflictsAsync(flight, disruption, cancellationToken);
        impacts.AddRange(gateConflicts);

        await _context.CascadeImpacts.AddRangeAsync(impacts, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var context = await BuildCascadeContextAsync(flight, disruption, impacts, cancellationToken);

        return new CascadeResult(impacts, context);
    }

    private void ApplyDirectImpact(Flight flight, Disruption disruption)
    {
        switch (disruption.Type)
        {
            case DisruptionType.Delay:
                var delayMinutes = GetDelayMinutes(disruption.DetailsJson);
                flight.EstimatedTime = flight.ScheduledTime.AddMinutes(delayMinutes);
                flight.Status = FlightStatus.Delayed;
                _logger.LogInformation("Flight {FlightNumber} delayed by {Minutes} min, new estimated: {EstimatedTime}",
                    flight.FlightNumber, delayMinutes, flight.EstimatedTime);
                break;

            case DisruptionType.Cancellation:
                flight.Status = FlightStatus.Cancelled;
                _logger.LogInformation("Flight {FlightNumber} cancelled", flight.FlightNumber);
                break;

            case DisruptionType.GateChange:
                var newGateId = GetNewGateId(disruption.DetailsJson);
                flight.GateId = newGateId;
                _logger.LogInformation("Flight {FlightNumber} gate changed to {GateId}", flight.FlightNumber, newGateId);
                break;
        }
    }

    private async Task<List<CascadeImpact>> DetectGateConflictsAsync(
        Flight flight, Disruption disruption, CancellationToken cancellationToken)
    {
        var impacts = new List<CascadeImpact>();

        if (flight.GateId == null)
            return impacts;

        var effectiveTime = flight.EstimatedTime ?? flight.ScheduledTime;
        var buffer = TimeSpan.FromMinutes(_gateBufferMinutes);
        var flightStart = effectiveTime - buffer;
        var flightEnd = effectiveTime + buffer;

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var otherFlightsAtGate = await _context.Flights
            .Where(f => f.GateId == flight.GateId
                && f.Id != flight.Id
                && f.Status != FlightStatus.Cancelled
                && f.ScheduledTime >= today
                && f.ScheduledTime < tomorrow)
            .ToListAsync(cancellationToken);

        foreach (var other in otherFlightsAtGate)
        {
            var otherTime = other.EstimatedTime ?? other.ScheduledTime;
            var otherStart = otherTime - buffer;
            var otherEnd = otherTime + buffer;

            if (flightStart < otherEnd && flightEnd > otherStart)
            {
                var overlapStart = flightStart > otherStart ? flightStart : otherStart;
                var overlapEnd = flightEnd < otherEnd ? flightEnd : otherEnd;

                var details = JsonSerializer.Serialize(new
                {
                    conflictingFlightNumber = other.FlightNumber,
                    disruptedFlightNumber = flight.FlightNumber,
                    gateCode = flight.Gate?.Code,
                    overlapStartUtc = overlapStart.ToString("o"),
                    overlapEndUtc = overlapEnd.ToString("o"),
                    overlapMinutes = (int)(overlapEnd - overlapStart).TotalMinutes
                });

                impacts.Add(new CascadeImpact
                {
                    OrganizationId = disruption.OrganizationId,
                    DisruptionId = disruption.Id,
                    AffectedFlightId = other.Id,
                    ImpactType = CascadeImpactType.GateConflict,
                    Severity = Severity.Critical,
                    Details = details
                });

                _logger.LogWarning("Gate conflict detected: {Flight1} and {Flight2} at gate {Gate}",
                    flight.FlightNumber, other.FlightNumber, flight.Gate?.Code);
            }
        }

        return impacts;
    }

    private async Task<CascadeContext> BuildCascadeContextAsync(
        Flight flight, Disruption disruption, List<CascadeImpact> impacts, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var gates = await _context.Gates
            .Where(g => g.AirportId == flight.AirportId && g.IsActive)
            .ToListAsync(cancellationToken);

        var crews = await _context.GroundCrews
            .Where(c => c.AirportId == flight.AirportId)
            .ToListAsync(cancellationToken);

        var affectedFlightIds = impacts.Select(i => i.AffectedFlightId).ToHashSet();
        var affectedFlights = await _context.Flights
            .Where(f => affectedFlightIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        var disruptedFlight = new CascadeContextFlight(
            flight.Id,
            flight.FlightNumber,
            flight.Airline,
            flight.Origin,
            flight.Destination,
            flight.Direction,
            flight.FlightType,
            flight.Status,
            flight.ScheduledTime,
            flight.EstimatedTime,
            flight.Gate?.Code,
            flight.Crew?.Name,
            flight.TurnaroundPairId);

        var contextImpacts = impacts.Select(i => new CascadeContextImpact(
            i.AffectedFlightId,
            affectedFlights.TryGetValue(i.AffectedFlightId, out var af) ? af.FlightNumber : "Unknown",
            i.ImpactType,
            i.Severity,
            i.Details)).ToList();

        var contextGates = gates.Select(g => new CascadeContextGate(
            g.Id, g.Code, g.GateType, g.SizeCategory, g.IsActive)).ToList();

        var contextCrews = crews.Select(c => new CascadeContextCrew(
            c.Id, c.Name, c.ShiftStart, c.ShiftEnd, c.Status)).ToList();

        return new CascadeContext(
            disruptedFlight,
            disruption.Type,
            disruption.DetailsJson,
            contextImpacts,
            contextGates,
            contextCrews,
            new List<string>());
    }

    private static int GetDelayMinutes(string detailsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            if (doc.RootElement.TryGetProperty("delayMinutes", out var prop))
                return prop.GetInt32();
        }
        catch { }
        return 0;
    }

    private static Guid GetNewGateId(string detailsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            if (doc.RootElement.TryGetProperty("newGateId", out var prop))
                return prop.GetGuid();
        }
        catch { }
        return Guid.Empty;
    }
}
