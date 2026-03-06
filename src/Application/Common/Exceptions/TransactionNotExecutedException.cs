namespace Application.Common.Exceptions;

public class TransactionNotExecutedException : Exception
{
    public TransactionNotExecutedException(string message)
        : base($"Transaction was not executed. Exception: {message}") { }
}
