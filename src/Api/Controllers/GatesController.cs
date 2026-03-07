using Application.Features.Gates.CreateGate;
using Application.Features.Gates.DeleteGate;
using Application.Features.Gates.GetGates;
using Application.Features.Gates.ImportGates;
using Application.Features.Gates.UpdateGate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class GatesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DateTime? date)
    {
        return Ok(await Mediator.Send(new GetGatesQuery(date)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGateCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGateCommand command)
    {
        var result = await Mediator.Send(command with { Id = id });
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteGateCommand(id));
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromForm] IFormFile file)
    {
        var command = new ImportGatesCommand(file);
        var result = await Mediator.Send(command);
        return Ok(result);
    }
}
