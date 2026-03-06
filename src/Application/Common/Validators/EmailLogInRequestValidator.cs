using Application.Common.Models.User;

using FluentValidation;

namespace Application.Common.Validators;

public class EmailLogInRequestValidator : AbstractValidator<EmailLogInRequest>
{
    public EmailLogInRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required.");
    }
}
