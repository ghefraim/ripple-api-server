using Application.Features.Disruptions.CreateDisruption;
using Application.Features.Disruptions.GetDisruptionById;
using Application.Features.Disruptions.GetDisruptions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class DisruptionsController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDisruptionCommand command)
    {
        var result = await Mediator.Send(command);
        return AcceptedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetDisruptionByIdQuery(id));
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? date)
    {
        var result = await Mediator.Send(new GetDisruptionsQuery(date));
        return Ok(result);
    }
}
