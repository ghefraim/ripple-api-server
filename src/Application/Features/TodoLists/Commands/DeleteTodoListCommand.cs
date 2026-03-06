using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoLists.DeleteTodoList;

[Authorize(Roles = "Owner")]
public record DeleteTodoListCommand(Guid Id) : IRequest<Unit>;

public class DeleteTodoListCommandValidator : AbstractValidator<DeleteTodoListCommand>
{
    public DeleteTodoListCommandValidator()
    {
    
    }
}

public class DeleteTodoListCommandHandler(
    ApplicationDbContext context) : IRequestHandler<DeleteTodoListCommand, Unit>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Unit> Handle(DeleteTodoListCommand request, CancellationToken cancellationToken)
    {
        var todoList = await _context.TodoLists
            .Include(tl => tl.Items)
            .FirstOrDefaultAsync(tl => tl.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TodoList), request.Id);

        foreach (var item in todoList.Items)
        {
            _context.TodoItems.Remove(item);
        }

        _context.TodoLists.Remove(todoList);
        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
