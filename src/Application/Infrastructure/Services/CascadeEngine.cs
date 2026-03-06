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
    private readonly int _turnaroundWarningMinutes;
    private readonly int _turnaroundCriticalMinutes;

    public CascadeEngine(
        ApplicationDbContext context,
        ILogger<CascadeEngine> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _gateBufferMinutes = configuration.GetValue("CascadeEngine:GateBufferMinutes", 30);
        _turnaroundWarningMinutes = configuration.GetValue("CascadeEngine:TurnaroundWarningMinutes", 35);
        _turnaroundCriticalMinutes = configuration.GetValue("CascadeEngine:TurnaroundCriticalMinutes", 20);
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

        if (disruption.Type == DisruptionType.Delay)
        {
            var turnaroundBreaches = await DetectTurnaroundBreachAsync(flight, disruption, cancellationToken);
            impacts.AddRange(turnaroundBreaches);
        }

        var crewGaps = await DetectCrewGapsAsync(flight, disruption, impacts, cancellationToken);
        impacts.AddRange(crewGaps);

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

    private async Task<List<CascadeImpact>> DetectTurnaroundBreachAsync(
        Flight flight, Disruption disruption, CancellationToken cancellationToken)
    {
        var impacts = new List<CascadeImpact>();

        if (flight.TurnaroundPairId == null)
            return impacts;

        var pairedFlight = await _context.Flights
            .Include(f => f.Gate)
            .FirstOrDefaultAsync(f => f.Id == flight.TurnaroundPairId, cancellationToken);

        if (pairedFlight == null || pairedFlight.Status == FlightStatus.Cancelled)
            return impacts;

        // Turnaround = paired departure ScheduledTime minus disrupted arrival EstimatedTime
        var arrivalTime = flight.EstimatedTime ?? flight.ScheduledTime;
        var departureTime = pairedFlight.ScheduledTime;
        var originalTurnaround = (departureTime - flight.ScheduledTime).TotalMinutes;
        var newTurnaround = (departureTime - arrivalTime).TotalMinutes;

        if (newTurnaround < _turnaroundWarningMinutes)
        {
            var severity = newTurnaround < _turnaroundCriticalMinutes ? Severity.Critical : Severity.Warning;

            var details = JsonSerializer.Serialize(new
            {
                originalTurnaroundMinutes = (int)originalTurnaround,
                newTurnaroundMinutes = (int)newTurnaround,
                pairedFlightNumber = pairedFlight.FlightNumber,
                disruptedFlightNumber = flight.FlightNumber,
                gateCode = pairedFlight.Gate?.Code
            });

            impacts.Add(new CascadeImpact
            {
                OrganizationId = disruption.OrganizationId,
                DisruptionId = disruption.Id,
                AffectedFlightId = pairedFlight.Id,
                ImpactType = CascadeImpactType.TurnaroundBreach,
                Severity = severity,
                Details = details
            });

            _logger.LogWarning(
                "Turnaround breach: {DisruptedFlight} -> {PairedFlight}, turnaround reduced from {Original}min to {New}min (severity: {Severity})",
                flight.FlightNumber, pairedFlight.FlightNumber, (int)originalTurnaround, (int)newTurnaround, severity);

            // Push paired departure forward by the deficit (one level only)
            var deficit = _turnaroundWarningMinutes - newTurnaround;
            if (deficit > 0)
            {
                pairedFlight.EstimatedTime = departureTime.AddMinutes(deficit);
                _logger.LogInformation(
                    "Paired flight {FlightNumber} departure pushed to {EstimatedTime} (deficit: {Deficit}min)",
                    pairedFlight.FlightNumber, pairedFlight.EstimatedTime, (int)deficit);
            }
        }

        return impacts;
    }

    private async Task<List<CascadeImpact>> DetectCrewGapsAsync(
        Flight flight, Disruption disruption, List<CascadeImpact> existingImpacts, CancellationToken cancellationToken)
    {
        var impacts = new List<CascadeImpact>();

        // Collect all affected flight IDs (disrupted flight + any already-impacted flights)
        var affectedFlightIds = new HashSet<Guid> { flight.Id };
        foreach (var impact in existingImpacts)
            affectedFlightIds.Add(impact.AffectedFlightId);

        // Load affected flights with their crews
        var affectedFlights = await _context.Flights
            .Include(f => f.Crew)
            .Where(f => affectedFlightIds.Contains(f.Id) && f.CrewId != null)
            .ToListAsync(cancellationToken);

        if (affectedFlights.Count == 0)
            return impacts;

        var crewIds = affectedFlights.Select(f => f.CrewId!.Value).Distinct().ToList();

        // Load all crews at this airport for alternative availability check
        var allCrews = await _context.GroundCrews
            .Where(c => c.AirportId == flight.AirportId)
            .ToListAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Load all today's flights for crews involved (to detect time overlaps)
        var crewFlights = await _context.Flights
            .Where(f => f.CrewId != null
                && crewIds.Contains(f.CrewId.Value)
                && f.Status != FlightStatus.Cancelled
                && f.ScheduledTime >= today
                && f.ScheduledTime < tomorrow)
            .ToListAsync(cancellationToken);

        var processedCrews = new HashSet<Guid>();

        foreach (var af in affectedFlights)
        {
            if (af.Crew == null || processedCrews.Contains(af.CrewId!.Value))
                continue;

            processedCrews.Add(af.CrewId!.Value);
            var crew = af.Crew;
            var crewFlightList = crewFlights.Where(f => f.CrewId == crew.Id).ToList();

            // Check 1: Crew shift end before flight's new estimated time
            var flightTime = af.EstimatedTime ?? af.ScheduledTime;
            var shiftEndToday = today.Add(crew.ShiftEnd.ToTimeSpan());

            if (shiftEndToday < flightTime)
            {
                var gapMinutes = (int)(flightTime - shiftEndToday).TotalMinutes;
                var alternativesExist = allCrews.Any(c =>
                    c.Id != crew.Id
                    && c.Status != CrewStatus.OnBreak
                    && today.Add(c.ShiftEnd.ToTimeSpan()) >= flightTime
                    && today.Add(c.ShiftStart.ToTimeSpan()) <= flightTime);

                var severity = alternativesExist ? Severity.Warning : Severity.Critical;

                var details = JsonSerializer.Serialize(new
                {
                    crewName = crew.Name,
                    conflictingFlightNumbers = new[] { af.FlightNumber },
                    gapDurationMinutes = gapMinutes,
                    reason = "Crew shift ends before flight estimated time"
                });

                impacts.Add(new CascadeImpact
                {
                    OrganizationId = disruption.OrganizationId,
                    DisruptionId = disruption.Id,
                    AffectedFlightId = af.Id,
                    ImpactType = CascadeImpactType.CrewGap,
                    Severity = severity,
                    Details = details
                });

                _logger.LogWarning(
                    "Crew gap: {CrewName} shift ends at {ShiftEnd} but flight {Flight} estimated at {EstimatedTime} (gap: {Gap}min, severity: {Severity})",
                    crew.Name, crew.ShiftEnd, af.FlightNumber, flightTime, gapMinutes, severity);
            }

            // Check 2: Crew assigned to two flights that now overlap in time
            for (var i = 0; i < crewFlightList.Count; i++)
            {
                for (var j = i + 1; j < crewFlightList.Count; j++)
                {
                    var f1 = crewFlightList[i];
                    var f2 = crewFlightList[j];
                    var f1Time = f1.EstimatedTime ?? f1.ScheduledTime;
                    var f2Time = f2.EstimatedTime ?? f2.ScheduledTime;

                    var buffer = TimeSpan.FromMinutes(_gateBufferMinutes);
                    var f1Start = f1Time - buffer;
                    var f1End = f1Time + buffer;
                    var f2Start = f2Time - buffer;
                    var f2End = f2Time + buffer;

                    if (f1Start < f2End && f1End > f2Start)
                    {
                        // Only create impact if at least one of the flights is affected by this disruption
                        if (!affectedFlightIds.Contains(f1.Id) && !affectedFlightIds.Contains(f2.Id))
                            continue;

                        // Avoid duplicate impacts for the same pair
                        var pairKey = f1.Id.CompareTo(f2.Id) < 0 ? (f1.Id, f2.Id) : (f2.Id, f1.Id);
                        var alreadyReported = impacts.Any(imp =>
                            imp.ImpactType == CascadeImpactType.CrewGap
                            && imp.Details.Contains(f1.FlightNumber)
                            && imp.Details.Contains(f2.FlightNumber));

                        if (alreadyReported)
                            continue;

                        var overlapMinutes = (int)(
                            (f1End < f2End ? f1End : f2End) -
                            (f1Start > f2Start ? f1Start : f2Start)
                        ).TotalMinutes;

                        var alternativesExist = allCrews.Any(c =>
                            c.Id != crew.Id
                            && c.Status != CrewStatus.OnBreak
                            && today.Add(c.ShiftEnd.ToTimeSpan()) >= f2Time
                            && today.Add(c.ShiftStart.ToTimeSpan()) <= f1Time);

                        var severity = alternativesExist ? Severity.Warning : Severity.Critical;

                        var details = JsonSerializer.Serialize(new
                        {
                            crewName = crew.Name,
                            conflictingFlightNumbers = new[] { f1.FlightNumber, f2.FlightNumber },
                            gapDurationMinutes = overlapMinutes,
                            reason = "Crew assigned to overlapping flights"
                        });

                        // Impact on the later flight (the one that needs reassignment)
                        var impactedFlightId = f1Time > f2Time ? f1.Id : f2.Id;

                        impacts.Add(new CascadeImpact
                        {
                            OrganizationId = disruption.OrganizationId,
                            DisruptionId = disruption.Id,
                            AffectedFlightId = impactedFlightId,
                            ImpactType = CascadeImpactType.CrewGap,
                            Severity = severity,
                            Details = details
                        });

                        _logger.LogWarning(
                            "Crew overlap: {CrewName} assigned to {Flight1} and {Flight2} which now overlap (severity: {Severity})",
                            crew.Name, f1.FlightNumber, f2.FlightNumber, severity);
                    }
                }
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
