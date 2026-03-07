using System.Text.Json;
using System.Text.Json.Serialization;
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
public record SyncFlightsCommand : IRequest<SyncFlightsResponse>;

public record SyncFlightsResponse(
    int FlightsSynced,
    int GatesAssigned,
    int CrewsAssigned,
    DateTime SyncedAt,
    string? Error
);

public class SyncFlightsCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    IHttpClientFactory httpClientFactory,
    ISender mediator,
    ILogger<SyncFlightsCommandHandler> logger) : IRequestHandler<SyncFlightsCommand, SyncFlightsResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ISender Mediator = mediator;
    private readonly ILogger<SyncFlightsCommandHandler> _logger = logger;

    public async Task<SyncFlightsResponse> Handle(SyncFlightsCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new Application.Common.Exceptions.NotFoundException("AirportConfig", organizationId);

        if (airport.FlightDataSource != FlightDataSource.AviationApi)
            throw new InvalidOperationException("Flight sync is only available for Aviation API integration.");

        if (string.IsNullOrEmpty(airport.FlightDataSourceConfigJson))
            throw new InvalidOperationException("Aviation API configuration is missing. Please set your API key first.");

        var config = JsonSerializer.Deserialize<AviationApiConfig>(airport.FlightDataSourceConfigJson, JsonOptions);
        if (config == null || string.IsNullOrEmpty(config.ApiKey))
            throw new InvalidOperationException("Invalid Aviation API configuration. API key is required.");

        var totalSynced = 0;

        try
        {
            var client = _httpClientFactory.CreateClient();

            // Free tier: no date filter supported, returns current/recent flights
            // Fetch departures
            var depsUrl = $"http://api.aviationstack.com/v1/flights?access_key={config.ApiKey}&dep_iata={airport.IataCode}&limit=100";
            var depsResponse = await client.GetAsync(depsUrl, cancellationToken);

            if (depsResponse.IsSuccessStatusCode)
            {
                var depsJson = await depsResponse.Content.ReadAsStringAsync(cancellationToken);
                var depsResult = JsonSerializer.Deserialize<AviationStackResponse>(depsJson, JsonOptions);

                if (depsResult?.Data != null)
                {
                    foreach (var f in depsResult.Data)
                    {
                        totalSynced += await UpsertFlight(airport, f, FlightDirection.Departure, cancellationToken);
                    }
                }
            }
            else
            {
                _logger.LogWarning("AviationStack departures request failed: {Status}", depsResponse.StatusCode);
            }

            // Fetch arrivals
            var arrsUrl = $"http://api.aviationstack.com/v1/flights?access_key={config.ApiKey}&arr_iata={airport.IataCode}&limit=100";
            var arrsResponse = await client.GetAsync(arrsUrl, cancellationToken);

            if (arrsResponse.IsSuccessStatusCode)
            {
                var arrsJson = await arrsResponse.Content.ReadAsStringAsync(cancellationToken);
                var arrsResult = JsonSerializer.Deserialize<AviationStackResponse>(arrsJson, JsonOptions);

                if (arrsResult?.Data != null)
                {
                    foreach (var f in arrsResult.Data)
                    {
                        totalSynced += await UpsertFlight(airport, f, FlightDirection.Arrival, cancellationToken);
                    }
                }
            }
            else
            {
                _logger.LogWarning("AviationStack arrivals request failed: {Status}", arrsResponse.StatusCode);
            }

            airport.LastSyncedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            // Auto-assign gates and crews to newly synced flights
            var assignResult = await Mediator.Send(new AssignGatesCommand(), cancellationToken);

            return new SyncFlightsResponse(
                totalSynced,
                assignResult.GatesAssigned,
                assignResult.CrewsAssigned,
                airport.LastSyncedAt.Value,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync flights from AviationStack");
            return new SyncFlightsResponse(totalSynced, 0, 0, DateTime.UtcNow, ex.Message);
        }
    }

    private async Task<int> UpsertFlight(AirportConfig airport, AviationStackFlight apiFlght, FlightDirection direction, CancellationToken cancellationToken)
    {
        var flightNumber = apiFlght.Flight?.Iata;
        if (string.IsNullOrEmpty(flightNumber)) return 0;

        var scheduledTime = direction == FlightDirection.Departure
            ? apiFlght.Departure?.Scheduled
            : apiFlght.Arrival?.Scheduled;

        if (scheduledTime == null) return 0;

        // Check if flight already exists (by flight number + scheduled time)
        var existing = await _context.Flights
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f =>
                f.AirportId == airport.Id &&
                f.FlightNumber == flightNumber &&
                f.ScheduledTime == scheduledTime.Value &&
                !f.IsDeleted,
                cancellationToken);

        if (existing != null)
        {
            existing.Status = MapStatus(apiFlght.FlightStatus);
            existing.EstimatedTime = direction == FlightDirection.Departure
                ? apiFlght.Departure?.Estimated
                : apiFlght.Arrival?.Estimated;
            existing.ActualTime = direction == FlightDirection.Departure
                ? apiFlght.Departure?.Actual
                : apiFlght.Arrival?.Actual;
            return 0;
        }

        var flight = new Flight
        {
            OrganizationId = airport.OrganizationId,
            AirportId = airport.Id,
            FlightNumber = flightNumber,
            Airline = apiFlght.Airline?.Name,
            Origin = direction == FlightDirection.Arrival ? apiFlght.Departure?.Iata : airport.IataCode,
            Destination = direction == FlightDirection.Departure ? apiFlght.Arrival?.Iata : airport.IataCode,
            Direction = direction,
            FlightType = DetermineFlightType(apiFlght, airport.IataCode),
            Status = MapStatus(apiFlght.FlightStatus),
            ScheduledTime = scheduledTime.Value,
            EstimatedTime = direction == FlightDirection.Departure
                ? apiFlght.Departure?.Estimated
                : apiFlght.Arrival?.Estimated,
            ActualTime = direction == FlightDirection.Departure
                ? apiFlght.Departure?.Actual
                : apiFlght.Arrival?.Actual,
        };

        _context.Flights.Add(flight);
        return 1;
    }

    private static FlightStatus MapStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "active" or "en-route" => FlightStatus.OnTime,
            "scheduled" => FlightStatus.OnTime,
            "landed" => FlightStatus.OnTime,
            "cancelled" => FlightStatus.Cancelled,
            "diverted" => FlightStatus.Diverted,
            "delayed" => FlightStatus.Delayed,
            _ => FlightStatus.OnTime,
        };
    }

    private static FlightType DetermineFlightType(AviationStackFlight flight, string airportIata)
    {
        var depIata = flight.Departure?.Iata;
        var arrIata = flight.Arrival?.Iata;
        if (depIata != null && arrIata != null && depIata.Length >= 2 && arrIata.Length >= 2)
        {
            // Romanian airports: OTP, CLJ, TSR, IAS, SBZ, BCM, CRA, SUJ, TGM, OMR, BAY
            var romanianAirports = new HashSet<string> { "OTP", "CLJ", "TSR", "IAS", "SBZ", "BCM", "CRA", "SUJ", "TGM", "OMR", "BAY" };
            if (romanianAirports.Contains(depIata) && romanianAirports.Contains(arrIata))
                return FlightType.Domestic;
        }
        return FlightType.International;
    }
}

// AviationStack API response models (snake_case JSON)
public class AviationStackResponse
{
    [JsonPropertyName("data")]
    public List<AviationStackFlight>? Data { get; set; }

    [JsonPropertyName("pagination")]
    public AviationStackPagination? Pagination { get; set; }
}

public class AviationStackPagination
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class AviationStackFlight
{
    [JsonPropertyName("flight_status")]
    public string? FlightStatus { get; set; }

    [JsonPropertyName("departure")]
    public AviationStackEndpoint? Departure { get; set; }

    [JsonPropertyName("arrival")]
    public AviationStackEndpoint? Arrival { get; set; }

    [JsonPropertyName("airline")]
    public AviationStackAirline? Airline { get; set; }

    [JsonPropertyName("flight")]
    public AviationStackFlightInfo? Flight { get; set; }
}

public class AviationStackEndpoint
{
    [JsonPropertyName("iata")]
    public string? Iata { get; set; }

    [JsonPropertyName("scheduled")]
    public DateTime? Scheduled { get; set; }

    [JsonPropertyName("estimated")]
    public DateTime? Estimated { get; set; }

    [JsonPropertyName("actual")]
    public DateTime? Actual { get; set; }

    [JsonPropertyName("gate")]
    public string? Gate { get; set; }

    [JsonPropertyName("terminal")]
    public string? Terminal { get; set; }
}

public class AviationStackAirline
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("iata")]
    public string? Iata { get; set; }
}

public class AviationStackFlightInfo
{
    [JsonPropertyName("iata")]
    public string? Iata { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }
}

public class AviationApiConfig
{
    public string? ApiKey { get; set; }
}
