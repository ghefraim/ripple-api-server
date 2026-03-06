using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Common.Validators;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItems.UpdateTodoItem;

[Authorize]
public record UpdateTodoItemCommand(
    Guid Id,
    string Title,
    string? Note,
    PriorityLevel Priority,
    DateTime? Reminder,
    DateTime? DueDate,
    Guid? AssignedToId
) : IRequest<UpdateTodoItemResponse>;

public record UpdateTodoItemResponse(
    Guid Id,
    Guid ListId,
    string Title,
    string? Note,
    PriorityLevel Priority,
    DateTime? Reminder,
    DateTime? DueDate,
    bool Done,
    Guid? AssignedToId,
    string? AssignedToName,
    DateTime CreatedOn,
    DateTime? UpdatedOn
);

public class UpdateTodoItemCommandValidator : AbstractValidator<UpdateTodoItemCommand>
{
    public UpdateTodoItemCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.")
            .NoHtml();

        RuleFor(x => x.Note)
            .MaximumLength(2000).WithMessage("Note must not exceed 2000 characters.")
            .NoHtml()
            .When(x => !string.IsNullOrEmpty(x.Note));

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Invalid priority level.");
    }
}

public class UpdateTodoItemCommandHandler(ApplicationDbContext context)
    : IRequestHandler<UpdateTodoItemCommand, UpdateTodoItemResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<UpdateTodoItemResponse> Handle(UpdateTodoItemCommand request, CancellationToken cancellationToken)
    {
        var todoItem = await _context.TodoItems
            .Include(ti => ti.AssignedTo)
            .FirstOrDefaultAsync(ti => ti.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TodoItem), request.Id);

        string? assignedToName = null;
        if (request.AssignedToId.HasValue)
        {
            var assignedUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.AssignedToId.Value, cancellationToken);

            if (assignedUser == null)
            {
                throw new NotFoundException("User", request.AssignedToId.Value);
            }
            assignedToName = assignedUser.UserName;
        }

        todoItem.Title = request.Title;
        todoItem.Note = request.Note;
        todoItem.Priority = request.Priority;
        todoItem.Reminder = request.Reminder;
        todoItem.DueDate = request.DueDate;
        todoItem.AssignedToId = request.AssignedToId;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateTodoItemResponse(
            todoItem.Id,
            todoItem.ListId,
            todoItem.Title!,
            todoItem.Note,
            todoItem.Priority,
            todoItem.Reminder,
            todoItem.DueDate,
            todoItem.Done,
            todoItem.AssignedToId,
            assignedToName,
            todoItem.CreatedOn,
            todoItem.UpdatedOn
        );
    }
}
