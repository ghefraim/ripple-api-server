using System.Text.Json;

using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rules.CreateRule;

[Authorize]
public record CreateRuleCommand(
    string Name,
    string? Description,
    string RuleJson
) : IRequest<CreateRuleResponse>;

public record CreateRuleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    string RuleJson);

public class CreateRuleCommandValidator : AbstractValidator<CreateRuleCommand>
{
    private static readonly HashSet<string> SupportedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "turnaround_minutes", "delay_minutes", "flight_type", "gate_type",
        "crew_status", "flight_status", "time_until_departure"
    };

    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "equals", "not_equals", "less_than", "greater_than", "in", "not_in"
    };

    private static readonly HashSet<string> SupportedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "flag_severity", "recommend", "auto_notify"
    };

    public CreateRuleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.RuleJson)
            .NotEmpty().WithMessage("RuleJson is required.")
            .Must(BeValidRuleJson).WithMessage("RuleJson must be valid JSON with 'conditions' and 'actions' arrays.");

        RuleFor(x => x.RuleJson)
            .Must(HaveValidConditionFields)
            .When(x => !string.IsNullOrEmpty(x.RuleJson))
            .WithMessage($"Conditions contain unsupported fields. Supported: {string.Join(", ", SupportedFields)}");

        RuleFor(x => x.RuleJson)
            .Must(HaveValidConditionOperators)
            .When(x => !string.IsNullOrEmpty(x.RuleJson))
            .WithMessage($"Conditions contain unsupported operators. Supported: {string.Join(", ", SupportedOperators)}");

        RuleFor(x => x.RuleJson)
            .Must(HaveValidActionTypes)
            .When(x => !string.IsNullOrEmpty(x.RuleJson))
            .WithMessage($"Actions contain unsupported types. Supported: {string.Join(", ", SupportedActions)}");
    }

    private static bool BeValidRuleJson(string? ruleJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(ruleJson);
            var root = doc.RootElement;
            return root.TryGetProperty("conditions", out var conditions) && conditions.ValueKind == JsonValueKind.Array
                && root.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array;
        }
        catch { return false; }
    }

    private static bool HaveValidConditionFields(string? ruleJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson)) return true;
        try
        {
            using var doc = JsonDocument.Parse(ruleJson);
            if (!doc.RootElement.TryGetProperty("conditions", out var conditions)) return true;
            foreach (var condition in conditions.EnumerateArray())
            {
                if (condition.TryGetProperty("field", out var field)
                    && !SupportedFields.Contains(field.GetString() ?? ""))
                    return false;
            }
            return true;
        }
        catch { return true; }
    }

    private static bool HaveValidConditionOperators(string? ruleJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson)) return true;
        try
        {
            using var doc = JsonDocument.Parse(ruleJson);
            if (!doc.RootElement.TryGetProperty("conditions", out var conditions)) return true;
            foreach (var condition in conditions.EnumerateArray())
            {
                if (condition.TryGetProperty("operator", out var op)
                    && !SupportedOperators.Contains(op.GetString() ?? ""))
                    return false;
            }
            return true;
        }
        catch { return true; }
    }

    private static bool HaveValidActionTypes(string? ruleJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson)) return true;
        try
        {
            using var doc = JsonDocument.Parse(ruleJson);
            if (!doc.RootElement.TryGetProperty("actions", out var actions)) return true;
            foreach (var action in actions.EnumerateArray())
            {
                if (action.TryGetProperty("type", out var type)
                    && !SupportedActions.Contains(type.GetString() ?? ""))
                    return false;
            }
            return true;
        }
        catch { return true; }
    }
}

public class CreateRuleCommandHandler(
    ApplicationDbContext context,
    ICurrentUserService currentUserService) : IRequestHandler<CreateRuleCommand, CreateRuleResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<CreateRuleResponse> Handle(CreateRuleCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var airport = await _context.AirportConfigs
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId, cancellationToken)
            ?? throw new NotFoundException("AirportConfig", organizationId);

        var rule = new OperationalRule
        {
            OrganizationId = organizationId,
            AirportId = airport.Id,
            Name = request.Name,
            Description = request.Description,
            RuleJson = request.RuleJson,
            IsActive = true,
            CreatedById = Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null,
        };

        _context.OperationalRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateRuleResponse(
            rule.Id,
            rule.Name,
            rule.Description,
            rule.IsActive,
            rule.RuleJson);
    }
}
