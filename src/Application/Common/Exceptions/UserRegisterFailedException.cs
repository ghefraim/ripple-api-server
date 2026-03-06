namespace Application.Common.Exceptions;

public class UserRegisterFailedException : Exception
{
    public UserRegisterFailedException()
        : base() { }

    public UserRegisterFailedException(string? message)
        : base(message)
    {
    }

    public UserRegisterFailedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}