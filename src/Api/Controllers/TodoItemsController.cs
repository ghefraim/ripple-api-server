using Application.Features.TodoItems.CreateTodoItem;
using Application.Features.TodoItems.DeleteTodoItem;
using Application.Features.TodoItems.ToggleTodoItemDone;
using Application.Features.TodoItems.UpdateTodoItem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class TodoItemsController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTodoItemCommand command)
    {
        return Ok(await Mediator.Send(command));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTodoItemCommand command)
    {
        return Ok(await Mediator.Send(command with { Id = id }));
    }

    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id)
    {
        return Ok(await Mediator.Send(new ToggleTodoItemDoneCommand(id)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteTodoItemCommand(id));
        return NoContent();
    }
}
