using Application.Features.Disruptions.Commands;
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
    public async Task<IActionResult> GetAll([FromQuery] DateTime? date, [FromQuery] bool includeArchived = false)
    {
        var result = await Mediator.Send(new GetDisruptionsQuery(date, includeArchived));
        return Ok(result);
    }

    [HttpPut("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id)
    {
        await Mediator.Send(new ArchiveDisruptionCommand(id));
        return NoContent();
    }

    [HttpPut("{disruptionId:guid}/action-plan/actions/{actionIndex:int}/status")]
    public async Task<IActionResult> UpdateActionStatus(Guid disruptionId, int actionIndex, [FromBody] UpdateActionStatusRequest request)
    {
        var result = await Mediator.Send(new UpdateActionStatusCommand(disruptionId, actionIndex, request.Status));
        return Ok(result);
    }

    [HttpPost("{disruptionId:guid}/action-plan/actions/{actionIndex:int}/execute")]
    public async Task<IActionResult> ExecuteAction(Guid disruptionId, int actionIndex)
        => Ok(await Mediator.Send(new ExecuteActionCommand(disruptionId, actionIndex)));
}
