namespace Zenmanage.Exceptions;

/// <summary>
/// Base exception type for SDK failures.
/// </summary>
public class ZenmanageException : Exception
{
    public ZenmanageException(string message)
        : base(message)
    {
    }

    public ZenmanageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}