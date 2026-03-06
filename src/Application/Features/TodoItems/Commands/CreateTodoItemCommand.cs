using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Common.Validators;
using Application.Domain.Entities;
using Application.Domain.Events;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoItems.CreateTodoItem;

[Authorize]
public record CreateTodoItemCommand(
    Guid ListId,
    string Title,
    string? Note,
    PriorityLevel Priority,
    DateTime? Reminder,
    DateTime? DueDate,
    Guid? AssignedToId
) : IRequest<CreateTodoItemResponse>;

public record CreateTodoItemResponse(
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
    DateTime CreatedOn
);

public class CreateTodoItemCommandValidator : AbstractValidator<CreateTodoItemCommand>
{
    public CreateTodoItemCommandValidator()
    {
        RuleFor(x => x.ListId)
            .NotEmpty().WithMessage("Invalid list ID.");

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

public class CreateTodoItemCommandHandler(ApplicationDbContext context)
    : IRequestHandler<CreateTodoItemCommand, CreateTodoItemResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<CreateTodoItemResponse> Handle(CreateTodoItemCommand request, CancellationToken cancellationToken)
    {
        var list = await _context.TodoLists
            .FirstOrDefaultAsync(tl => tl.Id == request.ListId, cancellationToken)
            ?? throw new NotFoundException(nameof(TodoList), request.ListId);

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

        var todoItem = new TodoItem
        {
            OrganizationId = list.OrganizationId,
            ListId = request.ListId,
            Title = request.Title,
            Note = request.Note,
            Priority = request.Priority,
            Reminder = request.Reminder,
            DueDate = request.DueDate,
            AssignedToId = request.AssignedToId,
            Done = false
        };

        todoItem.AddDomainEvent(new TodoItemCreatedEvent(todoItem));

        _context.TodoItems.Add(todoItem);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateTodoItemResponse(
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
            todoItem.CreatedOn
        );
    }
}
