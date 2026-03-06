using Application.Domain.Enums;
using Application.Features.Flights.GetFlightById;
using Application.Features.Flights.GetFlights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class FlightsController : ApiControllerBase
{
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
}
