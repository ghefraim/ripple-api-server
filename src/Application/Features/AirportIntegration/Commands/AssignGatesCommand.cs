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

        // Round-robin index per gate type group
        var domesticGates = gates.Where(g => g.GateType is GateType.Domestic or GateType.Both).ToList();
        var internationalGates = gates.Where(g => g.GateType is GateType.International or GateType.Both).ToList();
        var roundRobinIdx = new Dictionary<FlightType, int>
        {
            [FlightType.Domestic] = 0,
            [FlightType.International] = 0,
        };

        var assigned = 0;

        foreach (var flight in unassignedFlights)
        {
            var compatibleGates = flight.FlightType == FlightType.Domestic ? domesticGates : internationalGates;
            if (compatibleGates.Count == 0) continue;

            var rrIdx = roundRobinIdx[flight.FlightType];
            var flightStart = flight.ScheduledTime;
            var flightEnd = (flight.EstimatedTime ?? flight.ScheduledTime).Add(TurnaroundBuffer);

            Gate? bestGate = null;

            for (var i = 0; i < compatibleGates.Count; i++)
            {
                var candidateIdx = (rrIdx + i) % compatibleGates.Count;
                var candidate = compatibleGates[candidateIdx];
                var schedule = gateSchedules[candidate.Id];

                var hasConflict = schedule.Any(slot =>
                    flightStart < slot.End && flightEnd > slot.Start);

                if (!hasConflict)
                {
                    bestGate = candidate;
                    roundRobinIdx[flight.FlightType] = (candidateIdx + 1) % compatibleGates.Count;
                    break;
                }
            }

            if (bestGate == null) continue;

            flight.GateId = bestGate.Id;
            gateSchedules[bestGate.Id].Add((flightStart, flightEnd));
            assigned++;
        }

        return assigned;
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
