using Application.Common.Security;
using Application.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoLists.GetTodoLists;

[Authorize]
public record GetTodoListsQuery : IRequest<List<TodoListResponse>>;

public record TodoListResponse(
    Guid Id,
    string Title,
    string Colour,
    Guid OrganizationId,
    DateTime CreatedOn,
    string? CreatedBy,
    int ItemCount
);

public class GetTodoListsQueryHandler(ApplicationDbContext context)
    : IRequestHandler<GetTodoListsQuery, List<TodoListResponse>>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<List<TodoListResponse>> Handle(GetTodoListsQuery request, CancellationToken cancellationToken)
    {
        return await _context.TodoLists
            .Select(tl => new TodoListResponse(
                tl.Id,
                tl.Title!,
                tl.Colour,
                tl.OrganizationId,
                tl.CreatedOn,
                tl.CreatedBy,
                tl.Items.Count(i => !i.IsDeleted)
            ))
            .ToListAsync(cancellationToken);
    }
}
