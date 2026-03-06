using Application.Features.TodoLists.CreateTodoList;
using Application.Features.TodoLists.DeleteTodoList;
using Application.Features.TodoLists.UpdateTodoList;
using Application.Features.TodoLists.GetTodoListById;
using Application.Features.TodoLists.GetTodoLists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class TodoListsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await Mediator.Send(new GetTodoListsQuery()));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await Mediator.Send(new GetTodoListByIdQuery(id)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTodoListCommand command)
    {
        return Ok(await Mediator.Send(command));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTodoListCommand command)
    {
        return Ok(await Mediator.Send(command with { Id = id }));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteTodoListCommand(id));
        return NoContent();
    }
}
