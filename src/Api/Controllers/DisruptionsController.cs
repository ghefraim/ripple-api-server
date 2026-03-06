using Application.Features.Disruptions.CreateDisruption;
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
        return CreatedAtAction(nameof(Create), new { id = result.Id }, result);
    }
}
