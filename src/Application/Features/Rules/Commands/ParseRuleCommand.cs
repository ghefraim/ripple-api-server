using System.Text.Json;

using Application.Common.Interfaces;
using Application.Common.Security;

using FluentValidation;

using MediatR;

namespace Application.Features.Rules.ParseRule;

[Authorize]
public record ParseRuleCommand(string Text) : IRequest<ParseRuleResponse>;

public record ParseRuleResponse(
    bool Success,
    string? RuleJson,
    string? ErrorMessage);

public class ParseRuleCommandValidator : AbstractValidator<ParseRuleCommand>
{
    public ParseRuleCommandValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Text is required.")
            .MaximumLength(2000).WithMessage("Text must not exceed 2000 characters.");
    }
}

public class ParseRuleCommandHandler(ILlmProvider llmProvider) : IRequestHandler<ParseRuleCommand, ParseRuleResponse>
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

    public async Task<ParseRuleResponse> Handle(ParseRuleCommand request, CancellationToken cancellationToken)
    {
        var result = await llmProvider.ParseRuleAsync(request.Text, cancellationToken);

        if (!result.Success || string.IsNullOrEmpty(result.RuleJson))
        {
            return new ParseRuleResponse(false, null, result.ErrorMessage ?? "Failed to parse rule.");
        }

        var validationError = ValidateRuleSchema(result.RuleJson);
        if (validationError != null)
        {
            return new ParseRuleResponse(false, null, validationError);
        }

        return new ParseRuleResponse(true, result.RuleJson, null);
    }

    private static string? ValidateRuleSchema(string ruleJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(ruleJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("conditions", out var conditions) || conditions.ValueKind != JsonValueKind.Array)
                return "Parsed rule JSON missing 'conditions' array.";

            if (!root.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
                return "Parsed rule JSON missing 'actions' array.";

            foreach (var condition in conditions.EnumerateArray())
            {
                if (condition.TryGetProperty("field", out var field))
                {
                    var fieldValue = field.GetString() ?? "";
                    if (!SupportedFields.Contains(fieldValue))
                        return $"Unsupported field '{fieldValue}'. Supported: {string.Join(", ", SupportedFields)}";
                }

                if (condition.TryGetProperty("operator", out var op))
                {
                    var opValue = op.GetString() ?? "";
                    if (!SupportedOperators.Contains(opValue))
                        return $"Unsupported operator '{opValue}'. Supported: {string.Join(", ", SupportedOperators)}";
                }
            }

            foreach (var action in actions.EnumerateArray())
            {
                if (action.TryGetProperty("type", out var type))
                {
                    var typeValue = type.GetString() ?? "";
                    if (!SupportedActions.Contains(typeValue))
                        return $"Unsupported action type '{typeValue}'. Supported: {string.Join(", ", SupportedActions)}";
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return "Parsed rule JSON is not valid JSON.";
        }
    }
}
