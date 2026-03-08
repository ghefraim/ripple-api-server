using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Flights.ReassignFlight;

[Authorize]
public record ReassignFlightCommand(Guid FlightId, Guid? GateId, Guid? CrewId) : IRequest<ReassignFlightResponse>;

public record ReassignFlightRequest(Guid? GateId, Guid? CrewId);

public record ReassignFlightResponse(bool Success, string? GateCode, string? CrewName, string? Error);

public class ReassignFlightCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    ILogger<ReassignFlightCommandHandler> logger) : IRequestHandler<ReassignFlightCommand, ReassignFlightResponse>
{
    public async Task<ReassignFlightResponse> Handle(ReassignFlightCommand request, CancellationToken cancellationToken)
    {
        var organizationId = currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var flight = await context.Flights
            .Include(f => f.Gate)
            .Include(f => f.Crew)
            .FirstOrDefaultAsync(f => f.Id == request.FlightId, cancellationToken)
            ?? throw new NotFoundException("Flight", request.FlightId);

        string? gateCode = flight.Gate?.Code;
        string? crewName = flight.Crew?.Name;

        if (request.GateId.HasValue)
        {
            var gate = await context.Gates
                .FirstOrDefaultAsync(g => g.Id == request.GateId.Value && g.IsActive, cancellationToken)
                ?? throw new NotFoundException("Gate", request.GateId.Value);

            flight.GateId = gate.Id;
            gateCode = gate.Code;
            logger.LogInformation("Reassigned flight {FlightNumber} to gate {GateCode}", flight.FlightNumber, gate.Code);
        }

        if (request.CrewId.HasValue)
        {
            var crew = await context.GroundCrews
                .FirstOrDefaultAsync(c => c.Id == request.CrewId.Value, cancellationToken)
                ?? throw new NotFoundException("GroundCrew", request.CrewId.Value);

            flight.CrewId = crew.Id;
            crewName = crew.Name;
            logger.LogInformation("Assigned crew {CrewName} to flight {FlightNumber}", crew.Name, flight.FlightNumber);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new ReassignFlightResponse(true, gateCode, crewName, null);
    }
}
