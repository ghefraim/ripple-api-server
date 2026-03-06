using FluentValidation;
using FluentValidation.Validators;

namespace Application.Common.Validators;

public static class NoHtmlValidatorExtension
{
    public static IRuleBuilderOptions<T, string?> NoHtml<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder.SetValidator(new NoHtmlValidator<T>());
    }
}

public class NoHtmlValidator<T> : PropertyValidator<T, string?>
{
    public override string Name => "NoHtmlValidator";

    public override bool IsValid(ValidationContext<T> context, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        return !value.ContainsHtmlOrScript();
    }

    protected override string GetDefaultMessageTemplate(string errorCode)
        => "'{PropertyName}' must not contain HTML or script content.";
}
