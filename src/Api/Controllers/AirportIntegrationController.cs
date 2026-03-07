using Application.Features.AirportIntegration.Commands;
using Application.Features.AirportIntegration.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class AirportIntegrationController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await Mediator.Send(new GetAirportIntegrationQuery()));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateAirportIntegrationCommand command)
    {
        return Ok(await Mediator.Send(command));
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync()
    {
        return Ok(await Mediator.Send(new SyncFlightsCommand()));
    }

    [HttpPost("assign-gates")]
    public async Task<IActionResult> AssignGates()
    {
        return Ok(await Mediator.Send(new AssignGatesCommand()));
    }
}
