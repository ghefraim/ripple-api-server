using System.Text.Json;

using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Domain.Entities;
using Application.Infrastructure.Persistence;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Rules.UpdateRule;

[Authorize]
public record UpdateRuleCommand(
    Guid Id,
    string? Name,
    string? Description,
    string? RuleJson,
    bool? IsActive
) : IRequest<UpdateRuleResponse>;

public record UpdateRuleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    string RuleJson);

public class UpdateRuleCommandValidator : AbstractValidator<UpdateRuleCommand>
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

    public UpdateRuleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.")
            .When(x => x.Name is not null);

        RuleFor(x => x.RuleJson)
            .Must(BeValidRuleJson).WithMessage("RuleJson must be valid JSON with 'conditions' and 'actions' arrays.")
            .When(x => x.RuleJson is not null);

        RuleFor(x => x.RuleJson)
            .Must(HaveValidConditionFields)
            .When(x => x.RuleJson is not null)
            .WithMessage($"Conditions contain unsupported fields. Supported: {string.Join(", ", SupportedFields)}");

        RuleFor(x => x.RuleJson)
            .Must(HaveValidConditionOperators)
            .When(x => x.RuleJson is not null)
            .WithMessage($"Conditions contain unsupported operators. Supported: {string.Join(", ", SupportedOperators)}");

        RuleFor(x => x.RuleJson)
            .Must(HaveValidActionTypes)
            .When(x => x.RuleJson is not null)
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

public class UpdateRuleCommandHandler(
    ApplicationDbContext context) : IRequestHandler<UpdateRuleCommand, UpdateRuleResponse>
{
    private readonly ApplicationDbContext _context = context;

    public async Task<UpdateRuleResponse> Handle(UpdateRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _context.OperationalRules
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(OperationalRule), request.Id);

        if (request.Name is not null) rule.Name = request.Name;
        if (request.Description is not null) rule.Description = request.Description;
        if (request.RuleJson is not null) rule.RuleJson = request.RuleJson;
        if (request.IsActive.HasValue) rule.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateRuleResponse(
            rule.Id,
            rule.Name,
            rule.Description,
            rule.IsActive,
            rule.RuleJson);
    }
}
