namespace Application.Common.Exceptions;

public class UserLoginFailedException : Exception
{
    public UserLoginFailedException()
        : base() { }

    public UserLoginFailedException(string? message)
        : base(message)
    {
    }

    public UserLoginFailedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}