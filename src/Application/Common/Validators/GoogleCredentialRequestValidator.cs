using Application.Common.Models.User;

using FluentValidation;

namespace Application.Common.Validators;

public class GoogleCredentialRequestValidator : AbstractValidator<GoogleCredentialRequest>
{
    public GoogleCredentialRequestValidator()
    {
        RuleFor(x => x.Credential)
            .NotEmpty()
            .WithMessage("Google credential is required.");
    }
}
