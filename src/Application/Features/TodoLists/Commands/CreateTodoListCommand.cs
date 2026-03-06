using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Common.Validators;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TodoLists.CreateTodoList;

[Authorize(Roles = "Owner")]
public record CreateTodoListCommand(string Title, string? Colour) : IRequest<CreateTodoListResponse>;

public record CreateTodoListResponse(
    Guid Id,
    string Title,
    string Colour,
    Guid OrganizationId,
    DateTime CreatedOn
);

public class CreateTodoListCommandValidator : AbstractValidator<CreateTodoListCommand>
{
    private static readonly HashSet<string> SupportedColours = new(StringComparer.OrdinalIgnoreCase)
    {
        "#FFFFFF", "#FF5733", "#FFC300", "#FFFF66", "#CCFF99", "#6666FF", "#9966CC", "#999999"
    };

    public CreateTodoListCommandValidator()
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

public class CreateTodoListCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService,
    IEntitlementService entitlementService) : IRequestHandler<CreateTodoListCommand, CreateTodoListResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IEntitlementService _entitlementService = entitlementService;

    public async Task<CreateTodoListResponse> Handle(CreateTodoListCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        // Check entitlement limit before creating
        var currentCount = await _context.TodoLists
            .IgnoreQueryFilters()
            .CountAsync(tl => tl.OrganizationId == organizationId && !tl.IsDeleted, cancellationToken);

        var check = await _entitlementService.CheckLimitAsync("maxTodoLists", currentCount, cancellationToken);

        if (!check.IsAllowed)
        {
            throw new PaymentRequiredException(check.Message, "maxTodoLists", check.Limit, check.CurrentUsage);
        }

        var todoList = new TodoList
        {
            Title = request.Title,
            Colour = string.IsNullOrEmpty(request.Colour) ? "#FFFFFF" : request.Colour,
            OrganizationId = organizationId,
        };

        _context.TodoLists.Add(todoList);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateTodoListResponse(
            todoList.Id,
            todoList.Title!,
            todoList.Colour,
            todoList.OrganizationId,
            todoList.CreatedOn
        );
    }
}
