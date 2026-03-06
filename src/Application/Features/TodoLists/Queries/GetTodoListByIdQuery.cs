using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoLists.GetTodoListById;

[Authorize]
public record GetTodoListByIdQuery(Guid Id) : IRequest<TodoListDetailResponse>;

public record TodoListDetailResponse(
    Guid Id,
    string Title,
    string Colour,
    Guid OrganizationId,
    DateTime CreatedOn,
    string? CreatedBy,
    List<TodoItemDto> Items
);

public record TodoItemDto(
    Guid Id,
    string Title,
    string? Note,
    PriorityLevel Priority,
    DateTime? Reminder,
    DateTime? DueDate,
    bool Done,
    Guid? AssignedToId,
    string? AssignedToName,
    DateTime CreatedOn
);

public class GetTodoListByIdQueryHandler(ApplicationDbContext context)
    : IRequestHandler<GetTodoListByIdQuery, TodoListDetailResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<TodoListDetailResponse> Handle(GetTodoListByIdQuery request, CancellationToken cancellationToken)
    {
        var todoList = await _context.TodoLists
            .Include(tl => tl.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.AssignedTo)
            .FirstOrDefaultAsync(tl => tl.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TodoList), request.Id);

        return new TodoListDetailResponse(
            todoList.Id,
            todoList.Title!,
            todoList.Colour,
            todoList.OrganizationId,
            todoList.CreatedOn,
            todoList.CreatedBy,
            todoList.Items.Select(i => new TodoItemDto(
                i.Id,
                i.Title!,
                i.Note,
                i.Priority,
                i.Reminder,
                i.DueDate,
                i.Done,
                i.AssignedToId,
                i.AssignedTo?.UserName,
                i.CreatedOn
            )).ToList()
        );
    }
}
