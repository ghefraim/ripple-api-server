using Application.Common.Interfaces;
using Application.Domain.Enums;
using Application.Features.Flights.ExportFlights;
using Application.Features.Flights.GetFlightById;
using Application.Features.Flights.GetFlights;
using Application.Features.Flights.ImportFlights;
using Application.Features.Flights.ReassignFlight;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class FlightsController(ICsvFileBuilder csvFileBuilder) : ApiControllerBase
{
    private readonly ICsvFileBuilder _csvFileBuilder = csvFileBuilder;
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] FlightStatus? status)
    {
        return Ok(await Mediator.Send(new GetFlightsQuery(status)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await Mediator.Send(new GetFlightByIdQuery(id)));
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromForm] IFormFile file, [FromForm] string operationalDate)
    {
        if (!DateTime.TryParse(operationalDate, out var date))
        {
            return BadRequest("Invalid operational date format.");
        }
        
        var command = new ImportFlightsCommand(file, date);
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    [HttpPut("{id:guid}/reassign")]
    public async Task<IActionResult> Reassign(Guid id, [FromBody] ReassignFlightRequest request)
        => Ok(await Mediator.Send(new ReassignFlightCommand(id, request.GateId, request.CrewId)));

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] FlightStatus? status)
    {
        var records = await Mediator.Send(new ExportFlightsQuery(status));
        var fileContent = _csvFileBuilder.BuildFlightsFile(records);
        var fileName = $"flights-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        return File(fileContent, "text/csv", fileName);
    }
}
