using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Domain.Events;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItems.DeleteTodoItem;

[Authorize]
public record DeleteTodoItemCommand(Guid Id) : IRequest<Unit>;

public class DeleteTodoItemCommandValidator : AbstractValidator<DeleteTodoItemCommand>
{
    public DeleteTodoItemCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Invalid item ID.");
    }
}

public class DeleteTodoItemCommandHandler(ApplicationDbContext context)
    : IRequestHandler<DeleteTodoItemCommand, Unit>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Unit> Handle(DeleteTodoItemCommand request, CancellationToken cancellationToken)
    {
        var todoItem = await _context.TodoItems
            .FirstOrDefaultAsync(ti => ti.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TodoItem), request.Id);

        todoItem.AddDomainEvent(new TodoItemDeletedEvent(todoItem));

        _context.TodoItems.Remove(todoItem);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
