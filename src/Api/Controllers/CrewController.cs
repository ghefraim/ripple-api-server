using Application.Common.Interfaces;
using Application.Features.Crew.AddCrewContact;
using Application.Features.Crew.CreateCrew;
using Application.Features.Crew.DeleteCrew;
using Application.Features.Crew.DeleteCrewContact;
using Application.Features.Crew.ExportCrews;
using Application.Features.Crew.GetCrewById;
using Application.Features.Crew.GetCrews;
using Application.Features.Crew.ImportCrewContacts;
using Application.Features.Crew.ImportCrews;
using Application.Features.Crew.UpdateCrew;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class CrewController(ICsvFileBuilder csvFileBuilder) : ApiControllerBase
{
    private readonly ICsvFileBuilder _csvFileBuilder = csvFileBuilder;
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await Mediator.Send(new GetCrewsQuery()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await Mediator.Send(new GetCrewByIdQuery(id)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCrewCommand command)
    {
        var result = await Mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCrewCommand command)
    {
        if (id != command.Id)
            return BadRequest("Route id does not match command id.");

        var result = await Mediator.Send(command);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteCrewCommand(id));
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromForm] IFormFile file)
    {
        var command = new ImportCrewsCommand(file);
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var records = await Mediator.Send(new ExportCrewsQuery());
        var fileContent = _csvFileBuilder.BuildCrewsFile(records);
        var fileName = $"crews-export-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        return File(fileContent, "text/csv", fileName);
    }

    [HttpPost("{crewId:guid}/contacts")]
    public async Task<IActionResult> AddContact(Guid crewId, [FromBody] AddCrewContactCommand command)
    {
        var result = await Mediator.Send(command with { CrewId = crewId });
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpDelete("contacts/{contactId:guid}")]
    public async Task<IActionResult> DeleteContact(Guid contactId)
    {
        await Mediator.Send(new DeleteCrewContactCommand(contactId));
        return NoContent();
    }

    [HttpPost("{crewId:guid}/contacts/import")]
    public async Task<IActionResult> ImportContacts(Guid crewId, [FromForm] IFormFile file)
    {
        var result = await Mediator.Send(new ImportCrewContactsCommand(crewId, file));
        return Ok(result);
    }
}
