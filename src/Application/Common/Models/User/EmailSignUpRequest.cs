namespace Application.Common.Models.User;

public class EmailSignUpRequest : BaseAuthRequest
{
    public string ConfirmPassword { get; set; } = string.Empty;
}
