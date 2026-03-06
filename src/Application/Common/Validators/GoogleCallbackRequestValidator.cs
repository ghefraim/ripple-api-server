using Application.Common.Models.User;

using FluentValidation;

namespace Application.Common.Validators;

public class GoogleCallbackRequestValidator : AbstractValidator<GoogleCallbackRequest>
{
    public GoogleCallbackRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .WithMessage("Authorization code is required.");
    }
}
