using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.AirportIntegration.Commands;

[Authorize(Roles = "Owner")]
public record AssignGatesCommand : IRequest<AssignGatesResponse>;

public record AssignGatesResponse(
    int GatesAssigned,
    int CrewsAssigned,
    string? Error
);

public class AssignGatesCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    ILogger<AssignGatesCommandHandler> logger) : IRequestHandler<AssignGatesCommand, AssignGatesResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ILogger<AssignGatesCommandHandler> _logger = logger;

    private static readonly TimeSpan TurnaroundBuffer = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan PairedTurnaroundBuffer = TimeSpan.FromMinutes(15);

    public async Task<AssignGatesResponse> Handle(AssignGatesCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("AirportConfig", organizationId);

        try
        {
            var gatesAssigned = await AssignGates(airport.Id, cancellationToken);
            var crewsAssigned = await AssignCrews(airport.Id, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            return new AssignGatesResponse(gatesAssigned, crewsAssigned, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-assign gates/crews");
            return new AssignGatesResponse(0, 0, ex.Message);
        }
    }

    private async Task<int> AssignGates(Guid airportId, CancellationToken cancellationToken)
    {
        var gates = await _context.Gates
            .Where(g => g.AirportId == airportId && g.IsActive)
            .Include(g => g.Flights.Where(f => !f.IsDeleted))
            .OrderBy(g => g.Code)
            .ToListAsync(cancellationToken);

        if (gates.Count == 0) return 0;

        var unassignedFlights = await _context.Flights
            .Where(f => f.AirportId == airportId && f.GateId == null)
            .OrderBy(f => f.ScheduledTime)
            .ToListAsync(cancellationToken);

        if (unassignedFlights.Count == 0) return 0;

        // Build a lookup of already-assigned flights by ID for turnaround pair resolution
        var assignedFlightGateLookup = gates
            .SelectMany(g => g.Flights.Where(f => !f.IsDeleted).Select(f => new { f.Id, GateId = g.Id }))
            .ToDictionary(x => x.Id, x => x.GateId);

        // Track occupied time slots per gate: gate ID -> list of (start, end) tuples
        var gateSchedules = new Dictionary<Guid, List<(DateTime Start, DateTime End)>>();
        foreach (var gate in gates)
        {
            var slots = gate.Flights
                .Where(f => !f.IsDeleted)
                .Select(f => (
                    Start: f.ScheduledTime,
                    End: (f.EstimatedTime ?? f.ScheduledTime).Add(TurnaroundBuffer)
                ))
                .ToList();
            gateSchedules[gate.Id] = slots;
        }

        // Group unassigned flights into turnaround pairs and standalone flights
        var pairedGroups = unassignedFlights
            .Where(f => f.TurnaroundPairId != null)
            .GroupBy(f => f.TurnaroundPairId!.Value)
            .ToList();

        var pairedFlightIds = new HashSet<Guid>(
            pairedGroups.SelectMany(g => g.Select(f => f.Id)));

        var standaloneFlights = unassignedFlights
            .Where(f => !pairedFlightIds.Contains(f.Id))
            .ToList();

        // Round-robin index per gate type group
        var domesticGates = gates.Where(g => g.GateType is GateType.Domestic or GateType.Both).ToList();
        var internationalGates = gates.Where(g => g.GateType is GateType.International or GateType.Both).ToList();
        var gateById = gates.ToDictionary(g => g.Id);
        var roundRobinIdx = new Dictionary<FlightType, int>
        {
            [FlightType.Domestic] = 0,
            [FlightType.International] = 0,
        };

        var assigned = 0;

        // Process turnaround pairs first: arrival then departure on the same gate
        foreach (var group in pairedGroups)
        {
            var pairFlights = group.OrderBy(f => f.ScheduledTime).ToList();
            var arrival = pairFlights.FirstOrDefault(f => f.Direction == FlightDirection.Arrival)
                          ?? pairFlights.First();
            var departure = pairFlights.FirstOrDefault(f => f.Direction == FlightDirection.Departure && f.Id != arrival.Id);

            // Check if the turnaround partner is already assigned to a gate (seed data)
            Guid? forcedGateId = null;
            if (arrival.TurnaroundPairId.HasValue && assignedFlightGateLookup.TryGetValue(arrival.TurnaroundPairId.Value, out var partnerGateId))
                forcedGateId = partnerGateId;
            if (departure != null && departure.TurnaroundPairId.HasValue && !forcedGateId.HasValue
                && assignedFlightGateLookup.TryGetValue(departure.TurnaroundPairId.Value, out var partnerGateId2))
                forcedGateId = partnerGateId2;

            // Assign the arrival flight
            var arrivalGate = forcedGateId.HasValue
                ? AssignToForcedGate(arrival, forcedGateId.Value, gateById, gateSchedules, TurnaroundBuffer)
                : AssignToAvailableGate(arrival, domesticGates, internationalGates, gateSchedules, roundRobinIdx, TurnaroundBuffer);

            if (arrivalGate == null) continue;
            assigned++;

            // Force-assign the departure to the same gate with reduced buffer
            if (departure != null)
            {
                var departureAssigned = AssignToForcedGate(departure, arrivalGate.Id, gateById, gateSchedules, PairedTurnaroundBuffer);
                if (departureAssigned != null)
                    assigned++;
            }
        }

        // Process standalone (unpaired) flights with original 45-min buffer
        foreach (var flight in standaloneFlights)
        {
            var gate = AssignToAvailableGate(flight, domesticGates, internationalGates, gateSchedules, roundRobinIdx, TurnaroundBuffer);
            if (gate == null) continue;
            assigned++;
        }

        return assigned;
    }

    private static Gate? AssignToForcedGate(
        Flight flight,
        Guid gateId,
        Dictionary<Guid, Gate> gateById,
        Dictionary<Guid, List<(DateTime Start, DateTime End)>> gateSchedules,
        TimeSpan buffer)
    {
        if (!gateById.TryGetValue(gateId, out var gate)) return null;

        var flightStart = flight.ScheduledTime;
        var flightEnd = (flight.EstimatedTime ?? flight.ScheduledTime).Add(buffer);

        flight.GateId = gate.Id;
        gateSchedules[gate.Id].Add((flightStart, flightEnd));
        return gate;
    }

    private static Gate? AssignToAvailableGate(
        Flight flight,
        List<Gate> domesticGates,
        List<Gate> internationalGates,
        Dictionary<Guid, List<(DateTime Start, DateTime End)>> gateSchedules,
        Dictionary<FlightType, int> roundRobinIdx,
        TimeSpan buffer)
    {
        var compatibleGates = flight.FlightType == FlightType.Domestic ? domesticGates : internationalGates;
        if (compatibleGates.Count == 0) return null;

        var rrIdx = roundRobinIdx[flight.FlightType];
        var flightStart = flight.ScheduledTime;
        var flightEnd = (flight.EstimatedTime ?? flight.ScheduledTime).Add(buffer);

        for (var i = 0; i < compatibleGates.Count; i++)
        {
            var candidateIdx = (rrIdx + i) % compatibleGates.Count;
            var candidate = compatibleGates[candidateIdx];
            var schedule = gateSchedules[candidate.Id];

            var hasConflict = schedule.Any(slot =>
                flightStart < slot.End && flightEnd > slot.Start);

            if (!hasConflict)
            {
                flight.GateId = candidate.Id;
                gateSchedules[candidate.Id].Add((flightStart, flightEnd));
                roundRobinIdx[flight.FlightType] = (candidateIdx + 1) % compatibleGates.Count;
                return candidate;
            }
        }

        return null;
    }

    private async Task<int> AssignCrews(Guid airportId, CancellationToken cancellationToken)
    {
        var crews = await _context.GroundCrews
            .Where(c => c.AirportId == airportId && c.Status == CrewStatus.Available)
            .Include(c => c.AssignedFlights.Where(f => !f.IsDeleted))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        if (crews.Count == 0) return 0;

        var unassignedFlights = await _context.Flights
            .Where(f => f.AirportId == airportId && f.CrewId == null && f.GateId != null)
            .OrderBy(f => f.ScheduledTime)
            .ToListAsync(cancellationToken);

        if (unassignedFlights.Count == 0) return 0;

        // Track crew schedules
        var crewSchedules = new Dictionary<Guid, List<(DateTime Start, DateTime End)>>();
        foreach (var crew in crews)
        {
            var slots = crew.AssignedFlights
                .Where(f => !f.IsDeleted)
                .Select(f => (
                    Start: f.ScheduledTime,
                    End: (f.EstimatedTime ?? f.ScheduledTime).Add(TurnaroundBuffer)
                ))
                .ToList();
            crewSchedules[crew.Id] = slots;
        }

        var crewIdx = 0;
        var assigned = 0;

        foreach (var flight in unassignedFlights)
        {
            var flightTime = TimeOnly.FromDateTime(flight.ScheduledTime);
            var flightStart = flight.ScheduledTime;
            var flightEnd = (flight.EstimatedTime ?? flight.ScheduledTime).Add(TurnaroundBuffer);

            GroundCrew? bestCrew = null;

            for (var i = 0; i < crews.Count; i++)
            {
                var candidateIdx = (crewIdx + i) % crews.Count;
                var candidate = crews[candidateIdx];

                // Check if flight is within crew's shift
                var inShift = candidate.ShiftEnd > candidate.ShiftStart
                    ? flightTime >= candidate.ShiftStart && flightTime <= candidate.ShiftEnd
                    : flightTime >= candidate.ShiftStart || flightTime <= candidate.ShiftEnd; // overnight shift

                if (!inShift) continue;

                var schedule = crewSchedules[candidate.Id];
                var hasConflict = schedule.Any(slot =>
                    flightStart < slot.End && flightEnd > slot.Start);

                if (!hasConflict)
                {
                    bestCrew = candidate;
                    crewIdx = (candidateIdx + 1) % crews.Count;
                    break;
                }
            }

            if (bestCrew == null) continue;

            flight.CrewId = bestCrew.Id;
            crewSchedules[bestCrew.Id].Add((flightStart, flightEnd));
            assigned++;
        }

        return assigned;
    }
}
