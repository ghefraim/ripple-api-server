using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AirportIntegration.Commands;

[Authorize(Roles = "Owner")]
public record UpdateAirportIntegrationCommand(
    FlightDataSource FlightDataSource,
    string? ApiKey,
    bool? LlmEnabled
) : IRequest<UpdateAirportIntegrationResponse>;

public record UpdateAirportIntegrationResponse(
    FlightDataSource FlightDataSource,
    string? FlightDataSourceConfigJson,
    DateTime? LastSyncedAt,
    bool LlmEnabled
);

public class UpdateAirportIntegrationCommandValidator : AbstractValidator<UpdateAirportIntegrationCommand>
{
    public UpdateAirportIntegrationCommandValidator()
    {
        RuleFor(x => x.FlightDataSource)
            .IsInEnum().WithMessage("Invalid flight data source.");

        RuleFor(x => x.ApiKey)
            .NotEmpty().When(x => x.FlightDataSource == FlightDataSource.AviationApi)
            .WithMessage("API key is required for Aviation API integration.");
    }
}

public class UpdateAirportIntegrationCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<UpdateAirportIntegrationCommand, UpdateAirportIntegrationResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<UpdateAirportIntegrationResponse> Handle(UpdateAirportIntegrationCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("AirportConfig", organizationId);

        airport.FlightDataSource = request.FlightDataSource;

        if (request.FlightDataSource == FlightDataSource.AviationApi)
        {
            airport.FlightDataSourceConfigJson = System.Text.Json.JsonSerializer.Serialize(new { apiKey = request.ApiKey });
        }
        else if (request.FlightDataSource == FlightDataSource.Manual)
        {
            // Keep existing config but don't require it
        }

        if (request.LlmEnabled.HasValue)
        {
            airport.LlmEnabled = request.LlmEnabled.Value;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateAirportIntegrationResponse(
            airport.FlightDataSource,
            airport.FlightDataSourceConfigJson,
            airport.LastSyncedAt,
            airport.LlmEnabled
        );
    }
}
