using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Common.Validators;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoLists.UpdateTodoList;

[Authorize(Roles = "Owner")]
public record UpdateTodoListCommand(Guid Id, string Title, string? Colour) : IRequest<UpdateTodoListResponse>;

public record UpdateTodoListResponse(
    Guid Id,
    string Title,
    string Colour,
    Guid OrganizationId,
    DateTime CreatedOn,
    DateTime? UpdatedOn
);

public class UpdateTodoListCommandValidator : AbstractValidator<UpdateTodoListCommand>
{
    private static readonly HashSet<string> SupportedColours = new(StringComparer.OrdinalIgnoreCase)
    {
        "#FFFFFF", "#FF5733", "#FFC300", "#FFFF66", "#CCFF99", "#6666FF", "#9966CC", "#999999"
    };

    public UpdateTodoListCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.")
            .NoHtml();

        RuleFor(x => x.Colour)
            .Must(BeValidColour).When(x => !string.IsNullOrEmpty(x.Colour))
            .WithMessage("Invalid colour. Supported hex codes: #FFFFFF, #FF5733, #FFC300, #FFFF66, #CCFF99, #6666FF, #9966CC, #999999.");
    }

    private static bool BeValidColour(string? colour)
    {
        return colour == null || SupportedColours.Contains(colour);
    }
}

public class UpdateTodoListCommandHandler(
    ApplicationDbContext context) : IRequestHandler<UpdateTodoListCommand, UpdateTodoListResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<UpdateTodoListResponse> Handle(UpdateTodoListCommand request, CancellationToken cancellationToken)
    {
        var todoList = await _context.TodoLists
            .FirstOrDefaultAsync(tl => tl.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TodoList), request.Id);

        todoList.Title = request.Title;
        todoList.Colour = string.IsNullOrEmpty(request.Colour) ? "#FFFFFF" : request.Colour;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateTodoListResponse(
            todoList.Id,
            todoList.Title!,
            todoList.Colour,
            todoList.OrganizationId,
            todoList.CreatedOn,
            todoList.UpdatedOn
        );
    }
}
