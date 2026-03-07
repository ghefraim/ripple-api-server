using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AirportIntegration.Queries;

[Authorize(Roles = "Owner")]
public record GetAirportIntegrationQuery : IRequest<AirportIntegrationResponse>;

public record AirportIntegrationResponse(
    Guid AirportConfigId,
    string IataCode,
    string Name,
    FlightDataSource FlightDataSource,
    string? FlightDataSourceConfigJson,
    DateTime? LastSyncedAt
);

public class GetAirportIntegrationQueryHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<GetAirportIntegrationQuery, AirportIntegrationResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<AirportIntegrationResponse> Handle(GetAirportIntegrationQuery request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("AirportConfig", organizationId);

        return new AirportIntegrationResponse(
            airport.Id,
            airport.IataCode,
            airport.Name,
            airport.FlightDataSource,
            airport.FlightDataSourceConfigJson,
            airport.LastSyncedAt
        );
    }
}
