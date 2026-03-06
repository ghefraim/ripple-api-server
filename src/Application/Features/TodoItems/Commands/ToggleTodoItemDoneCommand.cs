using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItems.ToggleTodoItemDone;

[Authorize]
public record ToggleTodoItemDoneCommand(Guid Id) : IRequest<ToggleTodoItemDoneResponse>;

public record ToggleTodoItemDoneResponse(
    Guid Id,
    Guid ListId,
    string Title,
    bool Done,
    DateTime? UpdatedOn
);

public class ToggleTodoItemDoneCommandValidator : AbstractValidator<ToggleTodoItemDoneCommand>
{
    public ToggleTodoItemDoneCommandValidator()
    {
    
    }
}

public class ToggleTodoItemDoneCommandHandler(ApplicationDbContext context)
    : IRequestHandler<ToggleTodoItemDoneCommand, ToggleTodoItemDoneResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<ToggleTodoItemDoneResponse> Handle(ToggleTodoItemDoneCommand request, CancellationToken cancellationToken)
    {
        var todoItem = await _context.TodoItems
            .FirstOrDefaultAsync(ti => ti.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TodoItem), request.Id);

        // The Done setter will trigger TodoItemCompletedEvent if going from false to true
        todoItem.Done = !todoItem.Done;

        await _context.SaveChangesAsync(cancellationToken);

        return new ToggleTodoItemDoneResponse(
            todoItem.Id,
            todoItem.ListId,
            todoItem.Title!,
            todoItem.Done,
            todoItem.UpdatedOn
        );
    }
}
